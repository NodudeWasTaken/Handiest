using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MelonLoader;
using UnityEngine;
using HarmonyLib;
using RenderHeads.Media.AVProVideo;
using System.IO;
using Jayrock.Json;
using Jayrock.Json.Conversion;
using UnityEngine.EventSystems;
using System.Net;
using System.Text.RegularExpressions;
using System.Reflection;

[assembly: MelonInfo(typeof(Handiest.MyMod), "Handiest", "0.1", "Nodude")]
[assembly: MelonGame("Swearl, LLC", "PLAY'A Video Player")]

//Where are your warnings now HEHEHE
//#pragma warning disable CS0618

namespace Handiest {
	public class MyMod : MelonMod {
		public class ModConfig {
			public string HandyKey = null;
			public string ScriptsPath = ".";
			public bool useLocal = true;
			public string ipPrefix = "192.168.1.";
			public string serverUrl = "";

			//TODO: Generic check exists or default and save new entries
			//TODO: Also check valid values
			//TODO: Offset value

			//TODO: better server check method
			private Regex ip_regex = new Regex(@"(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})((\:\d+)?)");
			public string GetTrimmedUrl(string url=null) {
				if (url == null) {
					url = serverUrl;
				}
				if (url == null) {
					return null;
				}

				Match raw = ip_regex.Match(url);
				if (raw.Success) {
					return raw.Value;
				}

				return null;
			}
			public string FixUrl(string url) {
				string http = "http://";
				string https = "https://";

				//Default is https
				if (serverUrl.StartsWith(http) && url.StartsWith(https)) {
					return http + url.Substring(https.Length);
				}

				return url;
			}
			public bool ContainsServer(string url) {
				string server_url = MyMod.__instance.cfg.GetTrimmedUrl();

				if (server_url == null) {
					return false;
				}

				return url.Contains(server_url);
			}


			public JsonObject Serialize() {
				JsonObject dic = new JsonObject();
				dic.Add("HandyKey", this.HandyKey);
				dic.Add("ScriptsPath", this.ScriptsPath);
				dic.Add("useLocal", this.useLocal);
				dic.Add("ipPrefix", this.ipPrefix);
				dic.Add("serverUrl", this.serverUrl);
				return dic;
			}
			public void Deserialize(string data) {
				JsonObject dic = JSON.JsonObjectFromString(data);

				this.HandyKey = JSON.JsonDic_GetStr(dic, "HandyKey");
				this.ScriptsPath = JSON.JsonDic_GetStr(dic, "ScriptsPath");
				this.useLocal = JSON.JsonDic_GetBool(dic, "useLocal");
				this.ipPrefix = JSON.JsonDic_GetStr(dic, "ipPrefix");
				this.serverUrl = JSON.JsonDic_GetStr(dic, "serverUrl");
				if (String.IsNullOrEmpty(this.serverUrl)) {
					this.serverUrl = null;
				}
			}
		}

		//Shitty static reference hack
		public static MyMod __instance;

		//Variables
		private MediaPlayer player = null;
		private theHandy IHandy = null;
		private Dictionary<string, Uri> supplied_funscripts = new Dictionary<string, Uri>();
		public ModConfig cfg;

		//Current video information
		private string funscript_path;
		private CMediaItemDeo media_item;

		//Functions
		public void Msg(object msg) {
			__instance.LoggerInstance.Msg(msg);
		}

		public void SetVideoPlayer(MediaPlayer player) {
			this.player = player;
		}
		public void SetMediaItem(CMediaItemDeo media_item) {
			this.media_item = media_item;
		}
		public void SetFunscriptPath(string funscript_path) {
			this.funscript_path = funscript_path;
		}
		public string getMediaItemUniqueId(CMediaItemDeo mItm) {
			//TODO: Sanitize?
			return $"{mItm.GetID()}";
		}
		public void AddFunscript(string id, Uri funscript_url) {
			this.supplied_funscripts.Add(id, funscript_url);
		}
		
		public void VideoPause() {
			Msg("Video Pause");
			try {
				IHandy.Pause();
			} catch (Exception e) {
				MyMod.__instance.LoggerInstance.Error($"VideoPause: {e.ToString()}");
			}
		}
		public double VideoTime() {
			return player.Control.GetCurrentTime();
		}
		public void VideoPlay(double time) {
			Msg("Video Play");
			try { 
				IHandy.Play(time);
			} catch (Exception e) {
				MyMod.__instance.LoggerInstance.Error($"VideoPlay: {e.ToString()}");
			}
		}
		public void VideoPlay() {
			VideoPlay(VideoTime());
		}
		public void VideoStop() {
			Msg("Video Stop");
			try { 
				IHandy.Stop();
			} catch (Exception e) {
				MyMod.__instance.LoggerInstance.Error($"VideoStop: {e.ToString()}");
			}

			this.funscript_path = null;
			this.media_item = null;
		}
		public void VideoStart() {
			Msg("Video Start");
			if (this.funscript_path != null) {
				//Start local funscript
				try { 
					IHandy.Start(funscript_path);
				} catch (Exception e) {
					MyMod.__instance.LoggerInstance.Error($"VideoStart Local: {e.ToString()}");
				}
			} else if (this.media_item != null) {
				//Start remote funscript
				Uri funscript_url = null;
				string mediaId = this.getMediaItemUniqueId(this.media_item);
				MyMod.__instance.LoggerInstance.Msg($"Media ID: {mediaId}");
				if (this.supplied_funscripts.TryGetValue(mediaId, out funscript_url)) {
					try {
						IHandy.Start(funscript_url);
					} catch (Exception e) {
						MyMod.__instance.LoggerInstance.Error($"VideoStart Remote: {e.ToString()}");
					}
				}
			}
		}
		
		//Unity overrides
		public override void OnApplicationStart() {
			__instance = this;

			this.Msg("Hello :)");
			this.Msg("Am loading");

			//Check in documents for file (and android support)
#if ANDROID
			string path = $"/storage/emulated/0/Android/data/swearl-llc-play-a-video-player/files/UserData/handiest.json";
#else
			string path = Path.Combine(".", "handiest.json");
#endif

			LoggerInstance.Msg($"Config path: {path}");

			if (File.Exists(path)) {
				this.cfg = new ModConfig();
				this.cfg.Deserialize(File.ReadAllText(path));

				if (cfg.HandyKey == null) {
					LoggerInstance.Error($"Config {path} handy key is invalid!");
					throw new InvalidDataException($"Config {path} handy key is invalid!");
				}

			} else {
				LoggerInstance.Error($"No {path} file available, unable to find handy key!");
				
				ModConfig cfg = new ModConfig();
				File.WriteAllText(path, Tools.JsonPretty(cfg.Serialize()));

				throw new FileNotFoundException($"{path} created, please fill in the handy key!");
			}

			this.IHandy = new theHandy(cfg.HandyKey);
			WebServer.Start();

			//TODO: Reflection patch LogConf.isLogDeepLoadDataAsync to true
			//TODO: Reflection patch CLinks.isLogDeoLnk to true

			this.Msg("Loaded!");
		}
		public override void OnApplicationQuit() {
			this.Msg("Bye :)");

			//this.IHandy.Stop();
			//WebServer.Stop();
		}
	}

	//TODO: What happens if user downloads video?

	//TODO: Fix thumbnails?
	
	//TODO: Add buffering
	//See AVProVideo->MediaPlayerEvent->AddListener for events on the media player

	//TODO: Research C++ style infunction patching
	//See https://harmony.pardeike.net/articles/patching.html

	//DeoVR funscript support
	[HarmonyPatch(typeof(CDeoBlock), "UpdateFromDeoJson")]
	class UpdateFromDeoJsonPatch {
		public static void Postfix(JsonObject dic, string subKey, CMediaItemDeo owner, bool _isTrailer) {
			MyMod.__instance.LoggerInstance.Msg($"UpdateFromDeoJson: {dic}");
			
			JsonArray fleshlight; //ok boomer
			if (JSON.JD_TryGetArray(dic, "fleshlight", out fleshlight)) {
				foreach (JsonObject entry in fleshlight) {
					//TODO: I dont know why its an array
					//Assume first is correct
					MyMod.__instance.SetMediaItem(owner);
					var id = MyMod.__instance.getMediaItemUniqueId(owner);
					MyMod.__instance.LoggerInstance.Msg($"UpdateFromDeoJson ID: {id}");

					if (true) {
						MyMod.__instance.AddFunscript(id, new Uri(JSON.JsonDic_GetStr(entry, "url")));
					} else { 
						var url = WebServer.AddFile(JSON.JsonDic_GetStr(entry, "title"), new Uri(JSON.JsonDic_GetStr(entry, "url")));
						MyMod.__instance.AddFunscript(id, url);
					}
					break;
				}
			}
		}
	}

	//Fix missing categories
	//See CDataSync ParseNewVideosDeo
	//Last parsed list is set as the current list
	//TODO: Switchable via dropdown list
	[HarmonyPatch(typeof(CDataSync), "ParseNewVideosDeo")]
	class ParseNewVideosDeoPatch {
		public static List<CDeoScene> sceneEntries = new List<CDeoScene>();

		public static void Postfix(bool __result, JsonObject json, bool isLoadingMode, CSiteInfo sInfo) {
			if (!__result) { //Invalid scene
				return;
			}

			MyMod.__instance.LoggerInstance.Msg($"ParseNewVideosDeo called!: {sInfo.url}");
			if (MyMod.__instance.cfg.ContainsServer(sInfo.url)) {//TODO: Is this check necessary? 
				CDeoScene scene = null;
				foreach (CDeoScene scn in sceneEntries) {
					if (scn.sceneName == "Recent") {
						scene = scn;
						break;
					}
				}

				if (scene != null) {
					MyMod.__instance.LoggerInstance.Msg($"ParseNewVideosDeo selected: {scene.sceneName}");
					CStorage.share.UpdateVideosStart();
					CStorage.share.UpdateVideosFinish(scene.mList, null, null);
				}
			}
		}
	}
	//Video is trailer fix
	//TODO: Could do better by manual data fix instead of workaround
	[HarmonyPatch(typeof(CDeoScene), "ParseDetectScene")]
	class ParseDetectScenePatch {
		private static string sceneName;
		public static void Prefix(CSiteInfo sInfo, bool isAuthorized, ref string sceneName, JsonArray ja, ref CDeoScene scnFull, ref CDeoScene scnPrev) {
			if (MyMod.__instance.cfg.ContainsServer(sInfo.url)) {
				ParseDetectScenePatch.sceneName = sceneName;
				sceneName = "Full Videos";
			}
		}
		public static void Postfix(CSiteInfo sInfo, bool isAuthorized, string sceneName, JsonArray ja, ref CDeoScene scnFull, ref CDeoScene scnPrev) {
			if (MyMod.__instance.cfg.ContainsServer(sInfo.url)) {
				MyMod.__instance.LoggerInstance.Msg($"ParseDetectScene Matched: {sceneName} {scnFull != null} {scnPrev != null}");
				if (scnFull != null) {
					scnFull.sceneName = ParseDetectScenePatch.sceneName;
					ParseNewVideosDeoPatch.sceneEntries.Add(scnFull);
				}
				if (scnPrev != null) {
					scnPrev.sceneName = ParseDetectScenePatch.sceneName;
					ParseNewVideosDeoPatch.sceneEntries.Add(scnPrev);
				}
			} else {
				MyMod.__instance.LoggerInstance.Msg($"ParseDetectScene Unmatched: {sceneName}");
			}
		}
	}
	
	//Fix XBVR: Not supporting HEAD operation
	//See PanelPlayerControl->IReqUrlReqHeader->CRestTask_ReqHeader.StartSync
	[HarmonyPatch(typeof(HttpWebRequest), "GetResponse", new Type[] { })]
	class WebGetResponsePatch {
		public static void Prefix(HttpWebRequest __instance) {
			string request_url = __instance.RequestUri.ToString();

			//TODO: Replace with retry if failure with head
			if (MyMod.__instance.cfg.ContainsServer(request_url)) {
				if (__instance.Method == "HEAD") {
					MyMod.__instance.LoggerInstance.Msg("Replacing HEAD operation!");
					__instance.Method = "GET";
				}
			}
		}
	}

	//Fix XBVR: Patch change 0 value in resolution field
	//PLAY'A VR doesn't allow 0 in resolution field, as it tries to index videos by quality
	//So patch all 0 values to 1080p.
	[HarmonyPatch(typeof(CDeoBlock), "ParseEncoding", new Type[] {
		typeof(JsonObject),
		typeof(EVideoM),
		typeof(EVideoT),
		typeof(EVideoP),
		typeof(bool)
	})]
	class ParseEncodingPatch {
		public static void Prefix(CDeoBlock __instance, JsonObject dic, EVideoM evm, EVideoT evt, EVideoP evp, bool _isTrailers) {
			MyMod.__instance.LoggerInstance.Msg($"ParseEncoding Json: {Tools.JsonPretty(dic)}");

			try {
				JsonArray encode;
				JSON.JD_TryGetArray(dic, "encodings", out encode);
				foreach (JsonObject obj in encode) {
					JsonArray sources;
					JSON.JD_TryGetArray(obj, "videoSources", out sources);
					foreach (JsonObject obj2 in sources) {
						int res = JSON.JsonDic_GetInt32(obj2, "resolution");
						if (res == 0) {
							JSON.JsonDic_SetInt32(obj2, "resolution", 1080); //TODO: Lie better
						}
					}
				}

				MyMod.__instance.LoggerInstance.Msg($"ParseEncoding Json Patched: {Tools.JsonPretty(dic)}");
			} catch (Exception e) {
				MyMod.__instance.LoggerInstance.Msg($"ParseEncoding Error: {e}");
			}
		}
		public static void Postfix(CDeoBlock __instance, CLinks __result, JsonObject dic, EVideoM evm, EVideoT evt, EVideoP evp, bool _isTrailers) {
			MyMod.__instance.LoggerInstance.Msg($"ParseEncoding Json: {Tools.JsonPretty(dic)}");
		}
	}

	//Patch to add custom servers (the builtin keyboard doesn't allow colon)
	[HarmonyPatch(typeof(JSON), "LoadJsonArrayFromFile", new Type[] {
		typeof(string)
	})]
	class LoadJsonArrayFromFilePatch {
		public static void Postfix(JSON __instance, ref JsonArray __result, string filePath) {
			if (filePath.EndsWith("sites.jdat")) {
				bool found = false;

				if (MyMod.__instance.cfg.serverUrl == null) {
					return;
				}

				foreach (JsonObject obj in __result) {
					string url = JSON.JsonDic_GetStr(obj, "url");
					if (MyMod.__instance.cfg.ContainsServer(url)) {
						MyMod.__instance.LoggerInstance.Msg($"Found custom entry: {url}");
						found = true;
					}
				}

				if (!found) {
					MyMod.__instance.LoggerInstance.Msg($"Didnt find custom entry, adding");

					JsonObject dic = new JsonObject();
					dic.Add("url", MyMod.__instance.cfg.serverUrl);
					dic.Add("ent", "DEO");
					__result.Add(dic);
				}
			}
		}
	}

	//Allow HTTP
	//See CDataSync ITryGetApi for that hardcoded https
	//TODO: Research C++ style infunction patching (AGAIN)
	[HarmonyPatch(typeof(DXNet), "IReqUrlReqHeader", new Type[] {
		typeof(string),
		typeof(Action < ENetError, WebHeaderCollection >),
		typeof(int) })]
	class IReqUrlReqHeaderPatch {
		public static void Prefix(ref string url, Action<ENetError, WebHeaderCollection> callback, int redirectDeph) {
			if (MyMod.__instance.cfg.serverUrl == null) {
				return;
			}
			if (MyMod.__instance.cfg.ContainsServer(url)) {
				MyMod.__instance.LoggerInstance.Msg($"ReqHeader replace: {url}");
				url = MyMod.__instance.cfg.FixUrl(url);
				MyMod.__instance.LoggerInstance.Msg($"ReqHeader replaced: {url}");
			}
		}
	}
	//Allow HTTP
	//Uhh, CNetVRBase IReqGenerate i think?
	//TODO: Research C++ style infunction patching (AGAIN)
	[HarmonyPatch(typeof(CNetReqBase), "StartGet", new Type[] {
		typeof(string),
		typeof(Action<ENetError, JsonObject>) 
	})]
	class StartGetPatch {
		public static void Prefix(ref string url, Action<ENetError, JsonObject> callback) {
			if (MyMod.__instance.cfg.serverUrl == null) {
				return;
			}
			if (MyMod.__instance.cfg.ContainsServer(url)) {
				MyMod.__instance.LoggerInstance.Msg($"StartGet replace: {url}");
				url = MyMod.__instance.cfg.FixUrl(url);
				MyMod.__instance.LoggerInstance.Msg($"StartGet replaced: {url}");
			}
		}
	}
	
	//For local videos
	[HarmonyPatch(typeof(VRBCore), "LoadVideo", new Type[] {
		typeof(string),
		typeof(EVideoM),
		typeof(EVideoT),
		typeof(EVideoP)
	})]
	class LoadVideoPatch {
		public static void Postfix(VRBCore __instance, string path, EVideoM eVideoM, EVideoT eVideoT, EVideoP eVideoP) {
			MyMod.__instance.Msg($"VRBCore.LoadVideo");

			var video_player = __instance.display.Player;
			MyMod.__instance.SetVideoPlayer(video_player);
			MyMod.__instance.Msg("Playing local video!");
			MyMod.__instance.Msg($"Video path {path}");

			//Check path for funscript
			string funpath = Path.ChangeExtension(path, ".funscript");
			if (File.Exists(funpath)) {
				MyMod.__instance.Msg($"Has funscript at {funpath}");
				MyMod.__instance.SetFunscriptPath(funpath);
				MyMod.__instance.VideoStart();
			}
		}
	}

	//For API videos
	[HarmonyPatch(typeof(VRBCore), "LoadVideo", new Type[] {
		typeof(CMediaItem)
	})]
	class LoadVideoAltPatch {
		public static void Postfix(VRBCore __instance, CMediaItem mItm) {
			MyMod.__instance.Msg($"VRBCore.LoadVideo2");

			var video_player = __instance.display.Player;
			MyMod.__instance.SetVideoPlayer(video_player);
			MyMod.__instance.Msg("Playing remote video!");
			MyMod.__instance.Msg($"Video by \"{mItm.ownerName}\": \"{mItm.GetTitle()}\" - \"{mItm.GetID()}\"");
			MyMod.__instance.Msg($"JSON: {mItm.GetJson().ToString()}");

			//Set media item
			MyMod.__instance.SetMediaItem(mItm);

			//Check local filesystem for funscript (ownerName + id)
#if ANDROID
			string path = $"/storage/emulated/0/scripts/{MyMod.__instance.getMediaItemUniqueId(mItm)}";
#else
			string path = Path.Combine(MyMod.__instance.cfg.ScriptsPath, MyMod.__instance.getMediaItemUniqueId(mItm));
#endif

			string funpath = Path.ChangeExtension(path, ".funscript");
			if (File.Exists(funpath)) {
				MyMod.__instance.Msg($"Has funscript at {funpath}");
				MyMod.__instance.SetFunscriptPath(funpath);
				MyMod.__instance.VideoStart();
			}
		}
	}
	
	//Video player calls
	[HarmonyPatch(typeof(MediaPlayer), "Play")]
	class PlayPatch {
		public static void Postfix(MediaPlayer __instance) {
			MyMod.__instance.Msg($"MediaPlayer.Play");
			MyMod.__instance.VideoPlay();
		}
	}
	[HarmonyPatch(typeof(MediaPlayer), "Pause")]
	class PausePatch {
		public static void Postfix(MediaPlayer __instance) {
			MyMod.__instance.Msg($"MediaPlayer.Pause");
			MyMod.__instance.VideoPause();
		}
	}
	[HarmonyPatch(typeof(MediaPlayer), "Stop")]
	class StopPatch {
		public static void Postfix(MediaPlayer __instance) {
			MyMod.__instance.Msg($"MediaPlayer.Stop");
			MyMod.__instance.VideoStop();
		}
	}
	[HarmonyPatch(typeof(AVProExt), "SeekTo", new Type[] {
		typeof(float),
		typeof(bool),
		typeof(Action<float, float>)
	})]
	class SeekToPatch {
		public static void Postfix(AVProExt __instance, float timeNew, bool forLoading = false, Action<float, float> cbOnSeek = null) {
			MyMod.__instance.Msg($"AVProExt.SeekTo: {timeNew}");
			MyMod.__instance.VideoPlay(timeNew);
		}
	}

	//TODO: Does nothing?
	[HarmonyPatch(typeof(MediaPlayer), "OpenMedia", new Type[] {})]
	class OpenMediaPatch {
		public static void Postfix(MediaPlayer __instance, ref bool __result) {
			MyMod.__instance.Msg($"MediaPlayer.OpenMedia: {__result}");
			if (__result) {
				MyMod.__instance.VideoStart();
			}
		}
	}

	//Server video data reply overwrite
	/*[HarmonyPatch(typeof(CMediaItemDeo), MethodType.Constructor, new Type[] {
		typeof(JsonObject),
		typeof(ESiteType),
		typeof(bool),
		typeof(bool),
		typeof(bool),
		typeof(bool)
	})]
	class CMediaItemDeoPatch {
		public static void Postfix(CMediaItemDeo __instance, JsonObject dic, ESiteType owType, bool _isFromInfo, bool _isVrbData, bool _isDeoTrailers, bool _isPanic) {
			MyMod.__instance.Msg($"CMediaItemDeo Constructor: {Tools.JsonPretty(dic)}");
			
			//Check JSON dic for funscript and save
			if (JSON.JsonDic_IsHaveKey(dic, "funscript")) {
				Uri funscript_url = new Uri(JSON.JsonDic_GetStr(dic, "funscript"));
				MyMod.__instance.AddFunscript(MyMod.__instance.getMediaItemUniqueId(__instance), funscript_url);
			}
		}
	}*/
}
