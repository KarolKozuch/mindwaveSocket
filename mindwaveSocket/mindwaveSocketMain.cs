using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;

class mindwaveSocketMain
{

    private static TcpClient createSocket()
    {
        TcpClient client = null;

        string serverAddress = "127.0.0.1";
        Int32 port = 13854;

        client = new TcpClient(serverAddress, port);

        return client;
    }

    private static Stream sendConfig(TcpClient client)
    {
        byte[] message = Encoding.ASCII.GetBytes(@"{""enableRawOutput"":true,""format"":""Json""}");

        Stream s = client.GetStream();

        if (s.CanWrite)
        {
            s.Write(message, 0, message.Length);
        }

        return s;
    }

    private static TcpListener setupServer()
    {
        string serverAddress = "127.0.0.2";
        Int32 serverPort = 13000;

        TcpListener server = new TcpListener(IPAddress.Parse(serverAddress), serverPort);
        server.Start();

        return server;
    }

    /* Object for deserializing JSON */
    public class eegObj
    {
        public Int16 rawEeg;
    }

    public class Data
    {
        private List<Int16> eegRaw = new List<Int16>();
        private List<long> timestampsMs = new List<long>();

        public int writeData(Int16 raw, long timestamp)
        {
            eegRaw.Add(raw);
            timestampsMs.Add(timestamp);
            return eegRaw.Count;
        }
        public int addEegData(List<Int16> data)
        {
            foreach (Int16 record in data)
            {
                eegRaw.Add(record);
            }
            return eegRaw.Count;
        }
        public Int16[] readEegRaw()
        {
            return eegRaw.ToArray();
        }
        public long[] readTimestampsMs()
        {
            return timestampsMs.ToArray();
        }
        
        public void clean()
        {
            eegRaw.Clear();
            timestampsMs.Clear();
        }
    }

    private static eegObj convertData(string jsonData)
    {
        eegObj convertedData = null;
        try
        {
            convertedData = JsonConvert.DeserializeObject<eegObj>(jsonData);
            /* JSON parsed successfully */
            Console.WriteLine(convertedData?.rawEeg);
        }
        catch(Exception e)
        {
        }
        return convertedData;
    }

    private static float calculateMeanFreq(List<long> timestamps)
    {
        float avg = .0f;
        for(Int32 i = 0; i < timestamps.Count-1; i++)
        {
            avg += timestamps[i+1] - timestamps[i];
        }

        return (avg / timestamps.Count-1);
    }

    private static List<Int16> readPackets(Stream s)
    {
        List<Int16> eegValues = new List<Int16>();
        if(s.CanRead)
        {
            int bytesRead = 0;
            const Int16 bufferSize = 2048;
            byte[] buffer = new byte[bufferSize];
                
            bytesRead = s.Read(buffer, 0, bufferSize);

            string[] packets = Encoding.UTF8.GetString(buffer, 0, bytesRead).Split('\r');

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
    public static void Main()
    {
        Console.WriteLine("Hello TGC!");

        /* Create TCP clien socket */
        Console.WriteLine("Creating socket...");

        TcpClient client = createSocket();

        Console.WriteLine("Socket created.");

        /* Send config */

        Stream TG_stream = sendConfig(client);

        Console.WriteLine("Config sent");

        /* Start server for Matlab application */
        Console.WriteLine("Server setup");

        TcpListener server = setupServer();
        Console.WriteLine("Waiting for TCP connection from client...");

        TcpClient matlabApp = server.AcceptTcpClient();
        Console.WriteLine("Client connected");

        /* Perfom main loop of program:
            1. Start 0.5s timer to serve matlab app.
            2. Read data from TG - timestamps and RAW EEG.
            3. If timer elapsed, then send data to Matlab.
            4. Reset timer.
         */
        Console.WriteLine("Start readout of data");
        Stopwatch clientTimer = new Stopwatch();
        clientTimer.Start();
        Data data = new Data();
        while (true)
        {
            int noOfEegData = data.addEegData(readPackets(TG_stream));
            clientTimer.Stop();
            if(clientTimer.ElapsedMilliseconds > 100)
            {
                clientTimer.Reset();
                /* Send data to matlab */
                Stream s = matlabApp.GetStream();

                string packet = prepareMessage(data.readEegRaw());

                byte[] msg = System.Text.Encoding.ASCII.GetBytes(packet);
                s.Write(msg, 0, msg.Length);
                /* Clean buffer */
                data.clean();
            }
            clientTimer.Start();

        }
        client.Close();
        matlabApp.Close();
    }
}