using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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


    /* Object for deserializing JSON */
    public class eegObj
    {
        public Int16 rawEeg;
    }

    static class Data
    {
        public static Int16[] rawEeg = new Int16[20000];
        public static UInt64[] timestampMs = new UInt64[20000];
    }


    private static void convertData(string jsonData)
    {
        try
        {
            eegObj convertedData = JsonConvert.DeserializeObject<eegObj>(jsonData);
            /* JSON parsed successfully */
            Console.WriteLine(convertedData?.rawEeg);
        }
        catch(Exception e)
        {
        }

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

    private static void readPackets(Stream s)
    {
        if(s.CanRead)
        {
            int bytesRead = 0;
            const Int16 bufferSize = 2048;
            byte[] buffer = new byte[bufferSize];
            List<long> timestamps = new List<long>();
            Stopwatch timer = new Stopwatch();
            timer.Start();
            while (true)
            {
                
                bytesRead = s.Read(buffer, 0, bufferSize);

                string[] packets = Encoding.UTF8.GetString(buffer, 0, bytesRead).Split('\r');
                /* Update timestamps if packets read */
                if (0 < packets.Length)
                {
                    timer.Stop();
                    timestamps.Add(timer.ElapsedMilliseconds);
                    timer.Start();
                    calculateMeanFreq(timestamps);
                    Console.WriteLine(packets.Length);
                }
                foreach (string data in packets)
                {
                    convertData(data);
                }
            }
        }
    }

    public static void Main()
    {
        Console.WriteLine("Hello TGC!");

        /* Create TCP clien socket */
        Console.WriteLine("Creating socket...");

        TcpClient client = createSocket();

        Console.WriteLine("Socket created.");

        /* Send config */

        Stream stream = sendConfig(client);

        Console.WriteLine("Config sent");

        /* Read packets */

        Console.WriteLine("Reading packets:\n");

        readPackets(stream);

    }
}