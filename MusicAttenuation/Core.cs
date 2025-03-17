using HarmonyLib;
using Il2CppSLZ.Bonelab;
using Il2CppSLZ.Marrow.Audio;
using MelonLoader;
using System.Diagnostics;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;

[assembly: MelonInfo(typeof(MusicAttenuation.Core), "MusicAttenuation", "1.0.0", "notnotnotswipez", null)]
[assembly: MelonGame("Stress Level Zero", "BONELAB")]

namespace MusicAttenuation
{
    public class Core : MelonMod
    {
        static string prevLatestValue = "NONE";
        static string latestValue = "NONE";

        static string MUSIC_LISTENER_PROGRAM_FOLDER_PATH = Path.Combine(MelonUtils.UserDataDirectory, "MusicAttenuationProgram");
        static string MUSIC_LISTENER_PROGRAM_PATH = Path.Combine(MUSIC_LISTENER_PROGRAM_FOLDER_PATH, "MusicListener.exe");

        static string MUSIC_MIXER_NAME = "channel_Music";
        static float LAST_MUSIC_MIXER_VALUE = 0f;

        static bool isPlaying = false;
        static bool playerInitializedOnce = false;

        public override void OnInitializeMelon()
        {
            ExtractListenerIfPossible();
            RunListener();
            ConnectTCP();
        }

        private void ExtractListenerIfPossible() {
            if (!Directory.Exists(MUSIC_LISTENER_PROGRAM_FOLDER_PATH)) {
                Directory.CreateDirectory(MUSIC_LISTENER_PROGRAM_FOLDER_PATH);
            }

            if (!File.Exists(MUSIC_LISTENER_PROGRAM_PATH)) {
                Assembly assembly = Assembly.GetExecutingAssembly();
                string resourceName = assembly.GetManifestResourceNames().First(x => x.Contains("MusicListener"));

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    using (FileStream fileStream = File.Create(MUSIC_LISTENER_PROGRAM_PATH))
                        stream.CopyTo(fileStream);
                }
            }
        }

        public override void OnUpdate()
        {
            if (prevLatestValue != latestValue) {
                prevLatestValue = latestValue;

                if (latestValue.Contains("Playing"))
                {
                    if (playerInitializedOnce) {
                        ForceStopMusic();
                    }
                    isPlaying = true;
                }
                else {
                    isPlaying = false;

                    if (playerInitializedOnce) {
                        Audio3dManager.diegeticMusic.audioMixer.SetFloat(MUSIC_MIXER_NAME, LAST_MUSIC_MIXER_VALUE);
                    }
                }
            }
        }

        public static void ForceStopMusic() {
            Audio3dManager.diegeticMusic.audioMixer.GetFloat(MUSIC_MIXER_NAME, out var val);
            LAST_MUSIC_MIXER_VALUE = val;
            Audio3dManager.diegeticMusic.audioMixer.SetFloat(MUSIC_MIXER_NAME, -30f);
        }

        private void RunListener() {
            Thread runThread = new Thread(() => {
                Process process = new Process();
                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.Arguments = $"/c start \"\" \"{MUSIC_LISTENER_PROGRAM_PATH}\"";
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
            });

            runThread.Start();
        }

        private void ConnectTCP() {

            Thread connectThread = new Thread(() =>
            {
                using (TcpClient client = new TcpClient())
                {
                    client.Connect("127.0.0.1", 45565);

                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] buffer = new byte[1024];

                        while (client.Connected) {
                            int bytesRead = stream.Read(buffer, 0, buffer.Length);
                            latestValue = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        }
                    }
                }
            });
            
            connectThread.Start();
        }


        [HarmonyPatch(typeof(AudioMixer), nameof(AudioMixer.SetFloat))]
        public class AudioMixerFloatDebugPatch {

            public static bool Prefix(AudioMixer __instance, string name, float value) {
                if (name == MUSIC_MIXER_NAME && isPlaying && playerInitializedOnce) {
                    LAST_MUSIC_MIXER_VALUE = value;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(UIRig), nameof(UIRig.Awake))]
        public class UIRigAwakePatch
        {

            public static void Prefix(UIRig __instance)
            {
                if (!playerInitializedOnce && isPlaying)
                {
                    ForceStopMusic();
                }
                playerInitializedOnce = true;
            }
        }
    }
}