using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Threading;

namespace WebCamApp
{

    public partial class MainWindow : System.Windows.Window
    {
        private VideoCapture _capture;
        private Mat _frame;
        private bool _isCapturing;
        private bool _isRecording;
        private Thread _cameraThread;
        private VideoWriter _videoWriter;
        private string _videoFilePath;
        private CascadeClassifier _faceCascade;
        private string _selectedFilter = "None";
        private Mat _capturedImage;
        private bool _isSeeking = false;
        private DispatcherTimer _videoTimer;
        private bool _isVideoPlaying = false;
        private DispatcherTimer _recordingTimer;
        private DateTime _recordingStartTime;




        public MainWindow()
        {
            InitializeComponent();
            _faceCascade = new CascadeClassifier("haarcascade_frontalface_default.xml");
            _videoTimer = new DispatcherTimer();
            _videoTimer.Interval = TimeSpan.FromMilliseconds(500);
            _videoTimer.Tick += VideoTimer_Tick;
            _recordingTimer = new DispatcherTimer();
            _recordingTimer.Interval = TimeSpan.FromSeconds(1);
            _recordingTimer.Tick += RecordingTimer_Tick;

            StartCamera();
        }

        private void StartCamera()
        {
            _capture = new VideoCapture(0);
            if (!_capture.IsOpened())
            {
                MessageBox.Show("Camera not found.");
                return;
            }

            _frame = new Mat();
            _isCapturing = true;

            _cameraThread = new Thread(CaptureCamera) { IsBackground = true };
            _cameraThread.Start();
        }

        //Capture_Image
        private void CaptureCamera()
        {
            while (_isCapturing)
            {
                using (var tempFrame = new Mat())
                {
                    _capture.Read(tempFrame);
                    if (tempFrame.Empty()) continue;

                    _frame = tempFrame.Clone();

                    Cv2.Flip(tempFrame, tempFrame, FlipMode.Y);
                    ApplyFilter(tempFrame);
                    DetectFaces(tempFrame);

                    if (_isRecording && _videoWriter?.IsOpened() == true)
                        _videoWriter.Write(tempFrame);

                    var displayFrame = tempFrame.Clone();

                    Dispatcher.Invoke(() =>
                    {
                        WebcamImage.Source = BitmapSourceConverter.ToBitmapSource(displayFrame);
                    });
                }
                Thread.Sleep(33);
            }
        }

        private void DetectFaces(Mat frame)
        {
            var gray = new Mat();
            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
            var faces = _faceCascade.DetectMultiScale(gray, 1.1, 6);

            foreach (var face in faces)
            {
                Cv2.Rectangle(frame, face, Scalar.Green, 2);
            }
        }

        private void ApplyFilter(Mat frame)
{
    switch (_selectedFilter)
    {
        case "B&W":
            Cv2.CvtColor(frame, frame, ColorConversionCodes.BGR2GRAY);
            Cv2.CvtColor(frame, frame, ColorConversionCodes.GRAY2BGR); // Convert back for consistency
            break;

        case "Sepia":
            // Create sepia kernel manually
            Mat kernel = new Mat(3, 3, MatType.CV_32F);
            kernel.Set(0, 0, 0.272f); kernel.Set(0, 1, 0.534f); kernel.Set(0, 2, 0.131f);
            kernel.Set(1, 0, 0.349f); kernel.Set(1, 1, 0.686f); kernel.Set(1, 2, 0.168f);
            kernel.Set(2, 0, 0.393f); kernel.Set(2, 1, 0.769f); kernel.Set(2, 2, 0.189f);

            Mat sepiaFrame = new Mat();
            Cv2.Transform(frame, sepiaFrame, kernel);
            sepiaFrame.CopyTo(frame);
            sepiaFrame.Dispose();
            break;

        case "Insta":
            // Add a warm tone — example filter
            Cv2.Add(frame, new Scalar(10, 20, 30), frame);
            break;

        default:
            break;
    }
}


        private void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            if (_frame == null || _frame.Empty())
            {
                MessageBox.Show("❌ No frame to capture.");
                return;
            }

            // Clone the frame safely
            _capturedImage = _frame.Clone();

            // Pause live camera
            _isCapturing = false;

            // Safely update UI from UI thread
            Dispatcher.Invoke(() =>
            {
                try
                {
                    WebcamImage.Source = BitmapSourceConverter.ToBitmapSource(_capturedImage);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"⚠️ Failed to display captured image.\n\n{ex.Message}");
                }
            });
        }


        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_capturedImage != null)
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"Captured_{DateTime.Now:yyyyMMdd_HHmmss}",
                    DefaultExt = ".png",
                    Filter = "Image Files (*.png;*.jpg)|*.png;*.jpg"
                };

                if (dialog.ShowDialog() == true)
                {
                    Cv2.ImWrite(dialog.FileName, _capturedImage);
                    MessageBox.Show($"✅ Image saved to:\n{dialog.FileName}");
                }
            }
            else if (!string.IsNullOrEmpty(_videoFilePath) && File.Exists(_videoFilePath))
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = Path.GetFileName(_videoFilePath),
                    DefaultExt = ".mp4",
                    Filter = "Video Files (*.mp4)|*.mp4"
                };

                if (dialog.ShowDialog() == true)
                {
                    File.Copy(_videoFilePath, dialog.FileName, overwrite: true);
                    MessageBox.Show($"🎥 Video saved to:\n{dialog.FileName}");
                }
            }
            else
            {
                MessageBox.Show("⚠️ Nothing to save.");
            }
        }



        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isRecording)
            {
                _videoFilePath = $"Video_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
                _videoWriter = new VideoWriter(_videoFilePath, FourCC.H264, 30,
                    new OpenCvSharp.Size(_capture.FrameWidth, _capture.FrameHeight));

                if (!_videoWriter.IsOpened())
                {
                    MessageBox.Show("❌ Failed to start recording.");
                    return;
                }

                _isRecording = true;
                RecordButton.Content = "🟥 STOP";
                RecordButton.Background = System.Windows.Media.Brushes.Red;
                _recordingStartTime = DateTime.Now;
                _recordingTimer.Start();
                RecordingTimer.Text = "00:00";
                RecordingTimer.Visibility = Visibility.Visible;
            }
            else
            {
                _isRecording = false;
                _recordingTimer.Stop();
                RecordingTimer.Visibility = Visibility.Collapsed;
                _videoWriter?.Release();

                RecordButton.Content = "⏺ Record";
                RecordButton.Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF5722"));

                // Stop capturing camera
                _isCapturing = false;

                // Hide webcam image and show media player
                Dispatcher.Invoke(() =>
                {
                    WebcamImage.Visibility = Visibility.Collapsed;
                    VideoPlayer.Visibility = Visibility.Visible;
                    VideoControlPanel.Visibility = Visibility.Visible;

                    try
                    {
                        VideoPlayer.Source = new Uri(Path.GetFullPath(_videoFilePath));
                        VideoPlayer.LoadedBehavior = MediaState.Manual;
                        VideoPlayer.UnloadedBehavior = MediaState.Manual;

                        VideoPlayer.Play();
                        _isVideoPlaying = true;
                        PlayPauseButton.Content = "⏸";

                        _videoTimer.Start();

                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"⚠️ Failed to play video:\n\n{ex.Message}");
                    }
                });

            }
        }


        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(_videoFilePath))
            {
                MessageBox.Show("🎬 No video available.");
                return;
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _videoFilePath,
                UseShellExecute = true
            });
        }

        private void ResumeCamera_Click(object sender, RoutedEventArgs e)
        {
            if (!_isCapturing)
            {
                // Stop video playback and cleanup
                VideoPlayer.Stop();
                _isVideoPlaying = false;
                _videoTimer.Stop();
                PlayPauseButton.Content = "▶";

                // Hide video player and controls
                VideoPlayer.Visibility = Visibility.Collapsed;
                VideoControlPanel.Visibility = Visibility.Collapsed;

                // Show live camera feed again
                WebcamImage.Visibility = Visibility.Visible;

                _isCapturing = true;
                _cameraThread = new Thread(CaptureCamera) { IsBackground = true };
                _cameraThread.Start();
            }
        }




        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _isCapturing = false;
            _videoWriter?.Release();
            _capture?.Release();
        }

        private void FilterComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _selectedFilter = ((System.Windows.Controls.ComboBoxItem)FilterComboBox.SelectedItem).Content.ToString();
        }


        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            // Restart from beginning if at end
            if (VideoPlayer.Position >= VideoPlayer.NaturalDuration.TimeSpan)
            {
                VideoPlayer.Position = TimeSpan.Zero;
            }

            if (!_isVideoPlaying)
            {
                VideoPlayer.Play();
                _videoTimer.Start(); // Start syncing timer again
                _isVideoPlaying = true;
                PlayPauseButton.Content = "⏸";
            }
            else
            {
                VideoPlayer.Pause();
                _videoTimer.Stop(); // Pause syncing
                _isVideoPlaying = false;
                PlayPauseButton.Content = "▶";
            }
        }



        private void VideoTimer_Tick(object sender, EventArgs e)
        {
            if (!_isSeeking && VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                VideoSeekSlider.Maximum = VideoPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                VideoSeekSlider.Value = VideoPlayer.Position.TotalSeconds;

                var current = VideoPlayer.Position.ToString(@"mm\:ss");
                var total = VideoPlayer.NaturalDuration.TimeSpan.ToString(@"mm\:ss");
                VideoTimer.Text = $"{current} / {total}";
            }
        }



        private void VideoSeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Optional: for keyboard-only control
            if (VideoPlayer.NaturalDuration.HasTimeSpan && !_isSeeking)
            {
                VideoPlayer.Position = TimeSpan.FromSeconds(VideoSeekSlider.Value);
            }
        }


        private void VideoSeekSlider_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isSeeking = true;
        }

        private void VideoSeekSlider_PreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isSeeking = false;
            VideoPlayer.Position = TimeSpan.FromSeconds(VideoSeekSlider.Value);
        }


        private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            VideoPlayer.Stop();
            _isVideoPlaying = false;
            _videoTimer.Stop(); // Stop updating slider
            PlayPauseButton.Content = "▶";

            VideoPlayer.Position = TimeSpan.Zero;

        }




        private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            VideoSeekSlider.Maximum = VideoPlayer.NaturalDuration.TimeSpan.TotalSeconds;
        }

        private bool _blink = false;
        private void RecordingTimer_Tick(object sender, EventArgs e)
        {
            var elapsed = DateTime.Now - _recordingStartTime;
            Dispatcher.Invoke(() =>
            {
                _blink = !_blink;
                RecordingTimer.Text = (_blink ? "🔴 " : "⚪ ") + elapsed.ToString(@"mm\:ss");
            });
        }





    }
}