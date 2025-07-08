using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Threading;

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

        public MainWindow()
        {
            InitializeComponent();
            _faceCascade = new CascadeClassifier("haarcascade_frontalface_default.xml");
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
            var captured = _frame.Clone();
            WebcamImage.Source = BitmapSourceConverter.ToBitmapSource(captured);
            string imgPath = $"Captured_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            Cv2.ImWrite(imgPath, captured);
            MessageBox.Show($"📸 Image saved:\n{Path.GetFullPath(imgPath)}");
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_videoFilePath) && File.Exists(_videoFilePath))
            {
                MessageBox.Show($"💾 Video saved at:", Path.GetFullPath(_videoFilePath));
            }
        }

        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isRecording)
            {
                _videoFilePath = $"Video_{DateTime.Now:yyyyMMdd_HHmmss}.avi";
                _videoWriter = new VideoWriter(_videoFilePath, FourCC.XVID, 30,
                    new OpenCvSharp.Size(_capture.FrameWidth, _capture.FrameHeight));
                if (!_videoWriter.IsOpened())
                {
                    MessageBox.Show("❌ Failed to start recording.");
                    return;
                }

                _isRecording = true;
                RecordButton.Content = "🟥 STOP";
                RecordButton.Background = System.Windows.Media.Brushes.Red;
            }
            else
            {
                _isRecording = false;
                _videoWriter?.Release();
                RecordButton.Content = "⏺ Record";
                RecordButton.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF5722"));

                // Load preview
                Dispatcher.Invoke(() =>
                {
                    WebcamImage.Source = new BitmapImage(new Uri(Path.GetFullPath(_videoFilePath)));
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
                _isCapturing = true;
                _cameraThread = new Thread(CaptureCamera) { IsBackground = true };
                _cameraThread.Start();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _isCapturing = false;
            WebcamImage.Source = null;
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
    }
}