using System;
using System.Data;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.Capture;
using Windows.Media.Control;
using Windows.Media.Protection.PlayReady;

namespace Bonelab.ExternProgram
{
    internal class Program
    {
        static TcpListener listener;
        static TcpClient connectedClient;
        static GlobalSystemMediaTransportControlsSessionManager systemMediaControls;

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        static void Main(string[] args)
        {
            var handle = GetConsoleWindow();

            ShowWindow(handle, SW_HIDE);

            Thread thread = new Thread(() =>
            {
                listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 45565);
                listener.Start();

                connectedClient = listener.AcceptTcpClient();

                using (connectedClient)
                using (NetworkStream stream = connectedClient.GetStream())
                {
                    while (true)
                    {
                        if (systemMediaControls != null) {
                            var currentSession = systemMediaControls.GetCurrentSession();

                            if (currentSession != null)
                            {
                                byte[] responseData = Encoding.UTF8.GetBytes(currentSession.GetPlaybackInfo().PlaybackStatus.ToString());
                                stream.Write(responseData, 0, responseData.Length);
                            }
                        }
                        Thread.Sleep(400);
                    }
                }
            });
            thread.Start();

            AwaitAndHandleSession();

            
        }

        static async void AwaitAndHandleSession() {
            systemMediaControls = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        }
    }
}