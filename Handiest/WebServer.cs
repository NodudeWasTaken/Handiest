using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Handiest {
	class WebServer {
		private static HttpListener listener;
		private static Thread tWorker;
		private static Dictionary<string, string> dFiles = new Dictionary<string, string>();
		public readonly static int port = 2772;
		public readonly static string baseUrl = $"http://127.0.0.1:{port}/";
		public readonly static string lanUrl = $"http://{Tools.GetLocalIPAddress()}:{port}/";

		public static void Start() {
			// Create a listener.
			listener = new HttpListener();
			listener.Prefixes.Add(baseUrl);
			listener.Prefixes.Add(lanUrl);
			listener.Start();

			tWorker = new Thread(Worker);
			tWorker.Start();

			Console.WriteLine("WebServer Listening...");
		}
		public static Uri AddFile(string name, string data) {
			if (!dFiles.ContainsKey(name)) {
				dFiles.Add(name, data);
			}
			return new Uri($"{lanUrl}{name}");
		}
		private static void Worker() {
			while (listener.IsListening) {
				// Note: The GetContext method blocks while waiting for a request.
				HttpListenerContext context = listener.GetContext();
				HttpListenerRequest request = context.Request;

				// Obtain a response object.
				HttpListenerResponse response = context.Response;

				string data;
				if (dFiles.TryGetValue(request.RawUrl.Substring(1), out data)) {
					response.ContentType = "application/octet-stream";
					byte[] buffer = System.Text.Encoding.UTF8.GetBytes(data);
					response.StatusCode = 200;
					response.ContentLength64 = buffer.Length;
					System.IO.Stream output = response.OutputStream;
					output.Write(buffer, 0, buffer.Length);
					output.Close();
				} else {
					// Construct a response.
					string responseString = "<HTML><BODY>Hello world!</BODY></HTML>";
					byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
					// Get a response stream and write the response to it.
					response.StatusCode = 404;
					response.ContentLength64 = buffer.Length;
					System.IO.Stream output = response.OutputStream;
					output.Write(buffer, 0, buffer.Length);
					// You must close the output stream.
					output.Close();
				}
			}
		}
		public static void Stop() {
			listener.Stop();
		}
		public static void Main(string[] args) {
			dFiles.Add("memes.txt", "{\"funny\": \"data\"}");

			Start();
			Console.WriteLine("wait");
			Console.ReadLine();
			Stop();
		}
	}
}
