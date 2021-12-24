using Jayrock.Json;
using Jayrock.Json.Conversion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Assertions;
using UnityEngine.Networking;

namespace Handiest {
	class theHandy {
		private string urlApi;
		private string handyKey;
		private int serverOffset;
		private bool initialized = false;
		
		private async Task RequestWaiter(UnityWebRequest req) {
			while (!req.isDone) {
				await Task.Delay(10);
			}
		}

		private async Task<JsonObject> RequestAsync(string url, string bodyJsonString, string type="POST", string debug=null) {
			var request = new UnityWebRequest(url, type);
			if (debug != null) { MyMod.__instance.Msg($"{debug} Data: {bodyJsonString}");  }
			byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyJsonString);
			request.uploadHandler = new UploadHandlerRaw(bodyRaw);
			request.downloadHandler = new DownloadHandlerBuffer();

			request.SetRequestHeader("accept", "application/json");
			request.SetRequestHeader("X-Connection-Key", handyKey);
			request.SetRequestHeader("Content-Type", "application/json");

			//TODO: Cannot await?
			request.SendWebRequest();
			await this.RequestWaiter(request);

			Assert.AreEqual(request.responseCode, 200);

			//Encoding.UTF8.GetString
			JsonObject dic = JSON.JsonObjectFromString(request.downloadHandler.text);
			if (debug != null) { MyMod.__instance.Msg($"{debug} Return: {Tools.JsonPretty(dic)}"); }
			//TODO: If error in dict, fail
			return dic;
		}
		public theHandy(string handyKey) {
			this.urlApi = "https://www.handyfeeling.com/api/handy/v2";
			this.handyKey = handyKey;
			new Thread(new ThreadStart(delegate { this.UpdateServerTime(); })).Start();
		}
		private long GetServerTime() {
			return DateTimeOffset.Now.ToUnixTimeMilliseconds() + this.serverOffset;
		}
		private async void UpdateServerTime(int num=30) {
			long[] data = new long[num];

			for (var i = 0; i < num; i++) {
				var request = new UnityWebRequest(this.urlApi + "/servertime", "GET");
				request.downloadHandler = new DownloadHandlerBuffer();
				//TODO: Cannot await?

				long before = DateTimeOffset.Now.ToUnixTimeMilliseconds();
				request.SendWebRequest();
				while (!request.isDone) { } //Busy wait FTW

				Assert.AreEqual(request.responseCode, 200);
				long after = DateTimeOffset.Now.ToUnixTimeMilliseconds();
				long RTT = after - before;
				
				var dic = JSON.JsonObjectFromString(Encoding.UTF8.GetString(request.downloadHandler.data));
				long Ts = JSON.JsonDic_GetInt64(dic, "serverTime");

				long Ts_est = Ts + (RTT / 2);

				long offset = Ts_est - after;
				
				MyMod.__instance.Msg($"Time sync reply (num, rtt, this offset): {i}, {RTT}, {offset}");
				data[i] = offset;
			}

			serverOffset = 0;
			foreach (int i in data) {
				serverOffset += i;
			}
			serverOffset /= num;

			MyMod.__instance.Msg($"Sync: avg {serverOffset}");
		}
		public void Pause() {
			if (!initialized) {
				MyMod.__instance.Msg($"Pause Ignored");
				return;
			}

			this.RequestAsync(
				this.urlApi + "/hssp/stop", 
				"{}", 
				"PUT",
				"HSSP Pause"
			).ContinueWith((x) => {
				JsonObject dic = x.Result;
			});
		}
		public void Play(double seconds) {
			if (!initialized) {
				MyMod.__instance.Msg($"Play Ignored");
				return;
			}

			int millis = (int)(seconds * 1000.0);

			JsonObject ddic = new JsonObject();
			ddic.Add("estimatedServerTime", this.GetServerTime());
			ddic.Add("startTime", millis);

			this.RequestAsync(
				this.urlApi + "/hssp/play", 
				ddic.ToString(), 
				"PUT",
				"HSSP Play"
			).ContinueWith((x) => {
				JsonObject dic = x.Result;
				//Breaks with ERROR 4000 sometimes, when invalid funscript data is sent

			});
		}
		public void Stop() {
			this.Pause();
			this.initialized = false;
		}
		private Task<JsonObject> ModeSet() {
			JsonObject dic = new JsonObject();
			dic.Add("mode", 1);

			return this.RequestAsync(
				this.urlApi + "/mode", 
				dic.ToString(), 
				"PUT", 
				"ModeSet"
			);
		}
		public void Start(Uri url) {
			ModeSet().ContinueWith((_) => {
				JsonObject ddic = new JsonObject();
				ddic.Add("url", url.ToString());

				this.RequestAsync(
					this.urlApi + "/hssp/setup",
					ddic.ToString(),
					"PUT",
					"HSSP Setup (remote file)"
				).ContinueWith((x) => {
					JsonObject dic = x.Result;
					initialized = true;
				});
			});
		}
		private async Task<string> UploadAsync(string path) {
			string data = File.ReadAllText(path).Trim();
			data = Tools.FunscriptToCsv(data);
			string name = Regex.Replace(
				Path.GetFileName(
					Path.ChangeExtension(path, "")
				),
				"[^a-zA-Z0-9]",
				String.Empty
			) + ".csv";
			//string name = Path.GetFileName(path);

			if (MyMod.__instance.cfg.useLocal) {
				return WebServer.AddFile(name, data).ToString();
			}

			MyMod.__instance.Msg($"Script name: {name}");

			List<IMultipartFormSection> requestData = new List<IMultipartFormSection>();
			requestData.Add(new MultipartFormFileSection(
				"syncFile", 
				Encoding.UTF8.GetBytes(data),
				name,
				"text/csv"
			));

			var request = UnityWebRequest.Post(
				"https://www.handyfeeling.com/api/sync/upload?local=true", 
				requestData
			);
			request.SendWebRequest();
			await this.RequestWaiter(request);

			var dic = JSON.JsonObjectFromString(Encoding.UTF8.GetString(request.downloadHandler.data));

			MyMod.__instance.Msg($"Script Upload: {Tools.JsonPretty(dic)}");
			
			return JSON.JsonDic_GetStr(dic, "url");
		}
		public void Start(string path) {
			ModeSet().ContinueWith(async (_) => {
				string url;
				try {
					url = await this.UploadAsync(
						path
					);
				} catch (Exception e) {
					MyMod.__instance.LoggerInstance.Error(e.ToString());
					return;
				}

				JsonObject dic = new JsonObject();
				dic.Add("url", url);
				//dic.Add("sha256", Tools.ComputeSha256Hash(data));
				
				await this.RequestAsync(
					this.urlApi + "/hssp/setup",
					dic.ToString(),
					"PUT",
					"HSSP Setup (Local file)"
				).ContinueWith((x) => {
					initialized = true;
					Play(MyMod.__instance.VideoTime());
				});
			});
		}
		public void SetOffset(int ms) {
			JsonObject dic = new JsonObject();
			dic.Add("offset", ms);

			this.RequestAsync(
				this.urlApi + "/hssp/offset",
				dic.ToString(),
				"PUT",
				"HSSP Offset"
			);
		}
	}
}
