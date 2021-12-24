using Jayrock.Json;
using Jayrock.Json.Conversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Handiest {
	class Tools {
		/*public static class UnityWebRequestExtension {
			public static TaskAwaiter<UnityWebRequest.Result> GetAwaiter(this UnityWebRequestAsyncOperation reqOp) {
				TaskCompletionSource<UnityWebRequest.Result> tsc = new ();
				reqOp.completed += asyncOp => tsc.TrySetResult(reqOp.webRequest.result);

				if (reqOp.isDone)
					tsc.TrySetResult(reqOp.webRequest.result);

				return tsc.Task.GetAwaiter();
			}
		}*/
		public static string JsonPretty(JsonObject dic) {
			using (JsonTextWriter jw = new JsonTextWriter()) {
				jw.PrettyPrint = true;
				//jw.WriteStartObject();
				JsonConvert.Export(dic, jw);
				//jw.WriteEndObject();

				return jw.ToString();
			}
		}
		public static string ComputeSha256Hash(string rawData) {
			// Create a SHA256   
			using (SHA256 sha256Hash = SHA256.Create()) {
				// ComputeHash - returns byte array  
				byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

				// Convert byte array to a string   
				StringBuilder builder = new StringBuilder();
				for (int i = 0; i < bytes.Length; i++) {
					builder.Append(bytes[i].ToString("x2"));
				}
				return builder.ToString();
			}
		}
		public static string FunscriptToCsv(string script) {
			JsonObject dic = JSON.JsonObjectFromString(script);

			JsonArray acts;
			if (!JSON.JD_TryGetArray(dic, "actions", out acts)) {
				throw new JsonException("Missing entry actions in funscript!");
			}
			StringBuilder bob = new StringBuilder();

			foreach (JsonObject i in acts) {
				JsonObject obj = (JsonObject)i;
				long pos = JSON.JsonDic_GetInt64(obj, "pos");
				long at = JSON.JsonDic_GetInt64(obj, "at");
				bob.Append($"{at},{pos}\r\n");
			}

			return bob.ToString();
		}
		public static string GetLocalIPAddress() {
			var host = Dns.GetHostEntry(Dns.GetHostName());
			foreach (var ip in host.AddressList) {
				string address = ip.ToString();
				if (ip.AddressFamily == AddressFamily.InterNetwork && address.StartsWith(MyMod.__instance.cfg.ipPrefix)) {
					return address;
				}
			}
			throw new Exception("No network adapters with an IPv4 address in the system!");
		}
	}
}
