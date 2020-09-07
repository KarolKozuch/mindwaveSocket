using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;

/* Program do obsługi opaski MindWave Mobile 2 firmy Neurosky.
   Pośredniczy w wymianie danych między opaską a aplikacją Matlab,
   która korzysta z danych EEG RAW.

   Program odpowiada za nawiązanie połączenia z serwisem Windows 
   ThinkGear Connector, dostarczanym przez firmę Neurosky. Komunikacja
   odbywa się z wykorzystaniem socketów TCP/IP. Serwer dostępny jest
   pod adresem 127.0.0.1 i portem 13854. Więcej informacji nt działania
   serwis dostepnych jest na stronie neuroksy.com i w dokumentacji 
   ThinkGear Connector.

   Program przekazuje dane odczytane z opaski do klienta - aplikacji
   Matlab. Również wykorzystuje sockety TCP/IP, w tym przypadku
   pełniąc rolę serwera pod adresem 127.0.0.2 i portem 13000.

   Zadaniem programu jest odczyt, konwersja i udostpęnienie danych EEG
   w formacie zgodnym z aplikacją Matlab. Program powstał w celu
   poprawienia wydajności systemu.

   Autor: inż. Karol Kożuch
   Rok opracowania: 2020
    
   Wymagania.
   Framework: .NET Framework 4.8.2
   Dodatkowe paczki: 
        - JSON.NET, firma Newtonsoft,
        - System.Net
*/
class mindwaveSocketMain
{
    /* Definicja obiektu do przetwarzania paczek JSON. */
    public class eegObj
    {
        public Int16 rawEeg;
    }

    /*  Funkcja: eegObj convertData

        Przeznaczenie: Funkcja przetwarza pakiet jsonData odebrany od
        ThinkgGear Connector korzystając z biblioteki JSON.NET. 
        Jeśli pakiet zawiera pole o nazwie rawEeg, to zwracany zostaje 
        obiekt, zawierający tą wartość EEG RAW. Struktura pakietów JSON 
        określona jest przez dokumentację ThinkGear Connector.

        Argumenty funkcji: string jsonData - ciąg bajtów, odczytany ze strumienia
                           komunikacji z serwisem ThinkGear Connector

        Funkcja zwraca: eegObj convertedData - obiekt z 1 polem eegRaw, określający
                        wartosć pomiaru EEG RAW, dokonanego przez opaskę MindWave
                        Mobile 2. Jeśli pakiet JSON nie zawiera wartośći EEG RAW,
                        to zwracana jest wartość null.

        Zmienne globalne: brak

        Używane funkcje: 
            - JsonConvert.DeserializeObject<T> - funkcja mapująca pakiet JSON na obiekt T
    */
    private static eegObj convertData(string jsonData)
    {
        eegObj convertedData = null;
        try
        {
            convertedData = JsonConvert.DeserializeObject<eegObj>(jsonData);
            /* Wypisz dane w konsoli, jeśli pakiet zawiera dane eegRaw. */
            Console.WriteLine(convertedData?.rawEeg);
        }
        catch
        {
            /* Konwersja nieudana */
        }
        return convertedData;
    }

    /*  Funkcja: List<Int16> readPackets

    Przeznaczenie: Funkcja odczytująca bufor komunikacji z serwisem ThingGear Connector.
                   Odczytany ciag znaków dzielony jest znakiem '\r' - CR.
                   Następnie dla każdego ciągu znaków wywołwyana jest funkcja konwersji
                   pakietu JSON. Wg instrukcji ThinkGear Connector informacje 
                   wysyłane są w formacie JSON, zagregowanych w pakiety oddzielone
                   znakiem '\r'.
                   Jeśli funkcja przetwarzająca pakiet zwróci wartość inną niż null,
                   to zostaje ona dodana do listy eegValues, która zwracana jest na
                   końcu funkcji.

    Argumenty funkcji: Stream s - sturmień komunikacji z serwisem ThinkGear Connector.

    Funkcja zwraca: List<Int16> eegValues - lista wartości EEG RAW odczytanych ze strumienia s.

    Zmienne globalne: brak

    Używane funkcje: 
        - convertData - funkcja konwertująca pakiet JSON na obiekt eegObj.
    */
    private static List<Int16> readPackets(Stream s)
    {
        List<Int16> eegValues = new List<Int16>();
        if(s.CanRead)
        {
            int bytesRead = 0;
            const Int16 bufferSize = 2048;
            byte[] buffer = new byte[bufferSize];
            /* Odczyt bufora strumienia s */
            bytesRead = s.Read(buffer, 0, bufferSize);
            /* Podzielenie odczytanego ciągu znaków względem znaku CR ('\r') */
            string[] packets = Encoding.UTF8.GetString(buffer, 0, bytesRead).Split('\r');
            /* Konwersja odczytanych pakietów i aktualizacja bufora wartości EEG RAW */
            foreach (string data in packets)
            {
                eegObj value = convertData(data);
                if(null != value)
                {
                    eegValues.Add(value.rawEeg);
                }
            }
        }
        return eegValues;
    }

    /*  Funkcja: string prepareMessage

    Przeznaczenie: Funkcja przygotowujaca dane EEG RAW do wysłania.
                   Zamienia tablicę danych data na ciąg znaków. Wartości EEG RAW
                   oddzielone są znakiem '|'. Wartość zwracana jest gotowa do przesłania
                   poprzez socket TCP/IP.

    Argumenty funkcji: Int16[] data - tablica wartości pomiarów EEG RAW.

    Funkcja zwraca: string packet - ciąg wartości EEG RAW, dzielony '|', w formacie string.

    Zmienne globalne: brak

    Używane funkcje: brak

    */
    public static string prepareMessage(Int16[] data)
    {
        char delimiter = '|';
        string packet = "";
        foreach(Int16 value in data)
        {
            packet += value.ToString() + delimiter;
        }
        return packet;
    }

    /*  Funkcja: void Main

        Przeznaczenie: Funkcja główna programu do komunikacji
        z opaską MindWave Mobile 2, wykorzystującego serwis
        ThinkGear Connector (TGC).

        Argumenty funkcji: brak

        Funkcja zwraca: nic

        Zmienne globalne: brak

        Używane funkcje: 
            - createSocket - tworzenie socketu połączenia TCP/IP z serwisem TGC.
            - sendConfig - ustawienie parametrów komunikacji z serwisem TGC.
            - setupServer - uruchomienie serwera połączenia z aplikacją Matlab.
            - prepareMessage - przygotowanie danych do wysłania w pakiecie TCP/IP.
    */
    public static void Main()
    {
        Console.WriteLine("Program odczytu danych z ThinkgGear Connector");

        /* Utworzenie socketu typu klient połączenia z serwisem TGC. */
        Console.WriteLine("Tworzenie socketu...");
        TcpClient client = new TcpClient("127.0.0.1", 13854);
        Console.WriteLine("Socket utworzony");

        /* Ustawienie parametrów połaczenia z TGC. */
        byte[] message = Encoding.ASCII.GetBytes(@"{""enableRawOutput"":true,""format"":""Json""}");
        Stream TG_stream = client.GetStream();
        if (TG_stream.CanWrite)
        {
            TG_stream.Write(message, 0, message.Length);
        }
        Console.WriteLine("Konfiguracja ukończona");

        /* Utworzenie serwera połączenia z aplikacją Matlab */
        TcpListener server = new TcpListener(IPAddress.Parse("127.0.0.2"), 13000);
        server.Start();
        Console.WriteLine("Serwer utworzony. Adres: 127.0.0.2 Port: 13000");

        /* Oczekiwanie na połączenie z klientem */
        Console.WriteLine("Oczekiwanie na połączenie z klientem...");
        TcpClient matlabApp = server.AcceptTcpClient();
        Console.WriteLine("Klient połączony");
        /* Główna pętla programu */
        Console.WriteLine("Rozpoczęcie odczytu danych z ThinkGear Connector");
        /* Włączenie zegara do wysyłania danych co 100ms */
        Stopwatch clientTimer = new Stopwatch();
        clientTimer.Start();
        /* Bufor danych EEG RAW */
        List<Int16> eegRawData = new List<Int16>();
        try
        {
            while (true)
            {
                /* Odczyt danych z TGC i dodaj do listy pomiarów EEG RAW */
                foreach (Int16 record in readPackets(TG_stream))
                {
                    eegRawData.Add(record);
                }
                /* Sprawdzenie wartości timera (sekwencyjnie) */
                clientTimer.Stop();
                if (clientTimer.ElapsedMilliseconds > 100)
                {
                    clientTimer.Reset();
                    /* Wysyłanie danych do klienta (aplikacji Matlab) */
                    /* Pobranie strumienia połączenia z aplikacją Matlab */
                    Stream s = matlabApp.GetStream();
                    /* Przygotowanie danych do wysłania*/
                    string packet = prepareMessage(eegRawData.ToArray());
                    byte[] msg = System.Text.Encoding.ASCII.GetBytes(packet);
                    /* Wysłanie danych*/
                    s.Write(msg, 0, msg.Length);
                    /* Czyszczenie bufora danych EEG RAW */
                    eegRawData.Clear();
                }
                clientTimer.Start();
            }
        }
        catch
        {
            /* Błąd komunikacji, zakończ program. */
            client.Close();
            matlabApp.Close();
        }
    }
}