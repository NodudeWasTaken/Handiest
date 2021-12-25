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

			public JsonObject Serialize() {
				JsonObject dic = new JsonObject();
				dic.Add("HandyKey", this.HandyKey);
				dic.Add("ScriptsPath", this.ScriptsPath);
				dic.Add("useLocal", this.useLocal);
				dic.Add("ipPrefix", this.ipPrefix);
				return dic;
			}
			public void Deserialize(string data) {
				JsonObject dic = JSON.JsonObjectFromString(data);

				this.HandyKey = JSON.JsonDic_GetStr(dic, "HandyKey");
				this.ScriptsPath = JSON.JsonDic_GetStr(dic, "ScriptsPath");
				this.useLocal = JSON.JsonDic_GetBool(dic, "useLocal");
				this.ipPrefix = JSON.JsonDic_GetStr(dic, "ipPrefix");
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
			return $"{mItm.ownerName}-{mItm.GetID()}";
		}
		public void AddFunscript(string id, Uri funscript_url) {
			this.supplied_funscripts.Add(id, funscript_url);
			//TODO: Implement a server using this
		}
		
		public void VideoPause() {
			Msg("Video Pause");
			//this.playing = false;
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
			//this.playing = true;
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
			//this.playing = false;
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
			//this.playing = false
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

			this.Msg("Loaded!");
		}
		public override void OnApplicationQuit() {
			this.Msg("Bye :)");

			this.IHandy.Stop();
			WebServer.Stop();
		}
	}

	//TODO: Patch CBrowserWndData OnPress_Connect to allow direct ip connect?
	
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
	[HarmonyPatch(typeof(CMediaItemDeo), MethodType.Constructor, new Type[] {
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
	}
}
