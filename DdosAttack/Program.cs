using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace DdosAttack
{
    class Program
    {
        private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYabcdefghijklmnopqrstuvwxyz";
        private const string Url = "http://sceuchinfitbit.q4.local/Home/default.aspx";//
        private const int ContentLength = 512;
        private const int LargeContentLength = 65543 * 1024;
        private const int SleepIntervalInMs = 10000;
        private const int MinBytesPerSecond = 240;
        private const int TestNum = 5;

        static void Main(string[] args)
        {
            var success = false;

            for (var num = 4; num < TestNum; num++)
            {
                Console.WriteLine($"----- Test {num + 1}: Start -----");

                switch (num)
                {
                    case 0:
                        success = Test1();
                        break;
                    case 1:
                        success = Test2();
                        break;
                    case 2:
                        success = Test3();
                        break;
                    case 3:
                        success = Test4();
                        break;
                    case 4:
                        success = Test5();
                        break;
                }

                var result = success ? "SUCCESS" : "FAIL!!!";

                Console.WriteLine($"----- Test {num + 1}: {result} -----\n");
            }

            Console.ReadLine();
        }

        private static bool Test1()
        {
            Console.WriteLine("Testcase: Control test - Normal POST");

            var success = false;

            var stringData = "foo=bar&alpha=beta";
            var data = Encoding.ASCII.GetBytes(stringData);

            var request = CreateRequest(data.Length);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                using (var stream = request.GetRequestStream())
                {
                    Console.WriteLine($" Send {stringData}");
                    stream.Write(data, 0, data.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    var responseStream1 = response.GetResponseStream();

                    if (responseStream1 != null)
                    {
                        var responseString = new StreamReader(responseStream1).ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($" {ex}");
            }

            Console.WriteLine($" Time taken: {stopwatch.ElapsedMilliseconds / 1000}s");
            success = true;

            return success;
        }

        private static bool Test2()
        {
            Console.WriteLine("Testcase: 2 connections open at low bytes per second");

            var success = false;

            var request1 = CreateRequest(ContentLength);
            var request2 = CreateRequest(ContentLength);

            var stringData = new[] {"foo=bar", "alpha=beta"};
            var data = new byte[stringData.Length][];
            data[0] = Encoding.ASCII.GetBytes(stringData[0]);
            data[1] = Encoding.ASCII.GetBytes(stringData[1]);
            var a = Encoding.ASCII.GetBytes(new[] { 'a' });

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                using (var stream1 = request1.GetRequestStream())
                using (var stream2 = request2.GetRequestStream())
                {
                    var length = 0;
                    var alt = 0;
                    var idx = alt%2;

                    while (length + data[idx].Length <= ContentLength)
                    {
                        Console.WriteLine($" Send {stringData[idx]}");
                        stream1.Write(data[idx], 0, data[idx].Length);
                        stream2.Write(data[idx], 0, data[idx].Length);

                        length += data[idx].Length;
                        idx = ++alt%2;

                        Thread.Sleep(SleepIntervalInMs);
                    }

                    while (length < ContentLength)
                    {
                        stream1.Write(a, 0, a.Length);
                        stream2.Write(a, 0, a.Length);
                        length += a.Length;
                    }
                }

                using (var response1 = (HttpWebResponse) request1.GetResponse())
                using (var response2 = (HttpWebResponse) request2.GetResponse())
                {
                    var responseStream1 = response1.GetResponseStream();

                    if (responseStream1 != null)
                    {
                        var responseString = new StreamReader(responseStream1).ReadToEnd();
                    }

                    var responseStream2 = response2.GetResponseStream();

                    if (responseStream2 != null)
                    {
                        var responseString = new StreamReader(responseStream2).ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is WebException)
                {
                    var webException = (WebException) ex;
                    if (webException.Status == WebExceptionStatus.RequestCanceled)
                    {
                        Console.WriteLine(" Request cancelled");
                        if (stopwatch.ElapsedMilliseconds < 120000)
                        {
                            success = true;
                        }
                    }
                }
                else
                {
                    Console.WriteLine($" {ex}");
                }
            }

            Console.WriteLine($" Time taken: {stopwatch.ElapsedMilliseconds/1000}s");

            return success;
        }

        private static bool Test3()
        {
            Console.WriteLine("Testcase: Large message body");

            var success = false;

            var request = CreateRequest(LargeContentLength);

            var random = new Random(new DateTime().Millisecond);
            var stringData = new string(Enumerable.Repeat(Chars, LargeContentLength).Select(s => s[random.Next(s.Length)]).ToArray());
            var data = Encoding.ASCII.GetBytes(stringData);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                using (var stream = request.GetRequestStream())
                {
                    Console.WriteLine(" Send large message body");
                    stream.Write(data, 0, data.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    var responseStream1 = response.GetResponseStream();

                    if (responseStream1 != null)
                    {
                        var responseString = new StreamReader(responseStream1).ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is WebException)
                {
                    var webException = (WebException) ex;
                    if (((HttpWebResponse)webException.Response).StatusCode == HttpStatusCode.NotFound)
                    {
                        Console.WriteLine(" 404 Not Found");
                        success = true;
                    }
                }
                else
                {
                    Console.WriteLine($" {ex}");
                }
            }

            Console.WriteLine($" Time taken: {stopwatch.ElapsedMilliseconds / 1000}s");

            return success;
        }

        private static bool Test4()
        {
            Console.WriteLine("Testcase: Connections close within 5 mins");

            var success = false;

            var targetBytesPerSecond = 250;
            var minByteSeconds = 100; // min byte is checked every 100 secs
            var dataLength = targetBytesPerSecond * minByteSeconds;
            var mins = 10; // anything more than 5
            var contentLength = (int)Math.Ceiling((double)mins * 60 / 100) * dataLength;
            var request = CreateRequest(contentLength);

            var random = new Random(new DateTime().Millisecond);
            var stringData = new string(Enumerable.Repeat(Chars, dataLength).Select(s => s[random.Next(s.Length)]).ToArray());
            var data = Encoding.ASCII.GetBytes(stringData);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                using (var stream = request.GetRequestStream())
                {
                    var length = 0;

                    while (length + data.Length <= contentLength)
                    {
                        stream.Write(data, 0, data.Length);

                        length += data.Length;

                        Thread.Sleep((minByteSeconds - 10) * 1000); 

                        Console.WriteLine($" Time elapsed: {stopwatch.ElapsedMilliseconds / 60000} min");
                    }
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    var responseStream = response.GetResponseStream();

                    if (responseStream != null)
                    {
                        var responseString = new StreamReader(responseStream).ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is WebException)
                {
                    var webException = (WebException)ex;
                    if (webException.Status == WebExceptionStatus.RequestCanceled)
                    {
                        Console.WriteLine(" Request cancelled");
                        if (stopwatch.ElapsedMilliseconds < 5 * 60000) // less than 5 mins
                        {
                            success = true;
                        }
                    }
                }
                else
                {
                    Console.WriteLine($" {ex}");
                }
            }

            Console.WriteLine($" Time taken: {(double)stopwatch.ElapsedMilliseconds / 60000} min");

            return success;
        }

        private static bool Test5()
        {
            Console.WriteLine("Testcase: Connections close within 5 mins");

            var success = false;

            var targetBytesPerSecond = 250;
            var millisecInterval = 10;
            var dataLength = targetBytesPerSecond * 1000 / millisecInterval;
            var minsTotal = 6;
            var contentLength = (int)Math.Ceiling((double)minsTotal * 60000 / millisecInterval) * dataLength;
            var request = CreateRequest(contentLength);

            var random = new Random(new DateTime().Millisecond);
            var stringData = new string(Enumerable.Repeat(Chars, dataLength).Select(s => s[random.Next(s.Length)]).ToArray());
            var data = Encoding.ASCII.GetBytes(stringData);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                using (var stream = request.GetRequestStream())
                {
                    var length = 0;
                    var elapsedMs = stopwatch.ElapsedMilliseconds;

                    while (length + data.Length <= contentLength)
                    {
                        stream.Write(data, 0, data.Length);

                        length += data.Length;

                        Thread.Sleep((millisecInterval - 1) * 1000);

                        if (stopwatch.ElapsedMilliseconds - elapsedMs > 60000)
                        {
                            Console.WriteLine($" Time elapsed: {(double)stopwatch.ElapsedMilliseconds/60000} min");
                            elapsedMs = stopwatch.ElapsedMilliseconds;
                        }
                    }
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    var responseStream = response.GetResponseStream();

                    if (responseStream != null)
                    {
                        var responseString = new StreamReader(responseStream).ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is WebException)
                {
                    var webException = (WebException)ex;
                    if (webException.Status == WebExceptionStatus.RequestCanceled)
                    {
                        Console.WriteLine(" Request cancelled");
                        if (stopwatch.ElapsedMilliseconds < 5 * 60000) // less than 5 mins
                        {
                            success = true;
                        }
                    }
                }
                else
                {
                    Console.WriteLine($" {ex}");
                }
            }

            Console.WriteLine($" Time taken: {(double)stopwatch.ElapsedMilliseconds / 60000} min");

            return success;
        }

        private static HttpWebRequest CreateRequest(int contentLength)
        {
            var request = (HttpWebRequest)WebRequest.Create(Url);

            request.Method = "POST";
            request.UserAgent = "Mozilla/5.0 (compatible; MSIE 8.0; Windows NT 6.0;)";
            request.KeepAlive = false;
            request.Referer = "http://www.qualys.com/products/qg_suite/was/";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = contentLength;
            request.Accept = "text/html;q=0.9,text/plain;q=0.8,image/png,*/*;q=0.5";

            return request;
        }
    }
}
