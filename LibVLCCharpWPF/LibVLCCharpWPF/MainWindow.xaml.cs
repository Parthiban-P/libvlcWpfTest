using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using static System.Net.WebRequestMethods;
using System.Windows.Threading;
using System.Timers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static System.Net.Mime.MediaTypeNames;
using Application = System.Windows.Application;
using Image = System.Windows.Controls.Image;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;
using System.ComponentModel;
using System.Reflection;

namespace LibVLCCharpWPF
{
   
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        MediaPlayer _mediaPlayer;
        private LibVLC _libVlc;
        private readonly IList<MediaItem> _playlist = new List<MediaItem>();
        private int _currentIndex = 0;
        private int _videoCount = 0;
        private DispatcherTimer _timer = new DispatcherTimer();
        private VideoView videoView;

        private Media _oldMedia;
        private Media _newMedia;
        private MediaItem _currentItem;
        private readonly Image _image = new Image
        {
            Stretch = Stretch.Fill,
            StretchDirection = StretchDirection.Both
        };

        public class MediaItem
        {
            public Uri Uri { get; set; }
            public MediaType MediaType { get; set; }

            public MediaItem(Uri uri)
            {
                this.Uri = uri;
                MediaType = MediaType.Video;
            }

            public MediaItem(Uri uri, MediaType mediaType)
            {
                this.Uri = uri;
                MediaType = mediaType;
            }
        }

        public enum MediaType
        {
            Video ,
            Image
        }
       


        public MainWindow()
        {
            InitializeComponent();
            DirectoryInfo d = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)+ @"\Videos");

            var files = d.GetFiles("*.mp4");
            _videoCount = files.Length;
            foreach (FileInfo file in files)
            {
                _playlist.Add(new MediaItem(new Uri(file.FullName)));
            }

            files = d.GetFiles("*.png");
            _videoCount += files.Length;

            foreach (FileInfo file in files)
            {
                _playlist.Add(new MediaItem(new Uri(file.FullName), MediaType.Image));
            }

           
            _timer.Interval = TimeSpan.FromSeconds(9);
            _timer.Tick += timer_Tick;
           

            videoView = new VideoView();

            _libVlc = new LibVLC(enableDebugLogs: true);
            _mediaPlayer = new MediaPlayer(_libVlc);
            

            videoView.MediaPlayer = _mediaPlayer;
            videoView.HorizontalAlignment = HorizontalAlignment.Stretch;
            videoView.VerticalAlignment = VerticalAlignment.Stretch;
            //videoView.MediaPlayer.AspectRatio = $"{MediaContainer.Width} : {MediaContainer.Height}";

            _mediaPlayer.EndReached += VLCPlayer_EndReached;
            _mediaPlayer.EncounteredError += VLCPlayer_EncounteredError;

            // we need the VideoView to be fully loaded before setting a MediaPlayer on it.
            videoView.Loaded += (sender, e) =>
            {
                videoView.MediaPlayer = _mediaPlayer;
            };

            DequeueAndPlay();

            MediaContainer.IsVisibleChanged += MediaContainer_VisibleUpdated;
        }

        private void VLCPlayer_EncounteredError(object sender, EventArgs e)
        {
            DequeueAndPlay();
        }


        private void MediaContainer_VisibleUpdated(object sender, DependencyPropertyChangedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                if (videoView.MediaPlayer != null && _currentItem.MediaType == MediaType.Video)
                {
                    if (MediaContainer.Visibility == Visibility.Visible)
                    {
                        using (var media = new Media(_libVlc, _currentItem.Uri.AbsoluteUri, FromType.FromLocation))
                        {
                            media.AddOption("no-audio");
                            videoView.MediaPlayer.AspectRatio = $"{MediaContainer.ActualWidth}:{MediaContainer.ActualHeight}";
                            videoView.MediaPlayer.Fullscreen = true;
                            videoView?.MediaPlayer?.Play(media);
                        }
                    }
                }
            }));
        }

       

        void timer_Tick(object sender, EventArgs e)
        {
            _timer.Stop();
            DequeueAndPlay();
        }

        public void StartVideo(MediaItem item)
        {
                MediaContainer.Visibility = Visibility.Collapsed;
           
                _currentItem = item;
                MediaContainer.Children.Add(videoView);
                MediaContainer.Visibility = Visibility.Visible;
            
        }

        public void DisplayImage(MediaItem item)
        {
            MediaContainer.Visibility = Visibility.Collapsed;

            var myBitmapImage = new BitmapImage();
            // BitmapImage.UriSource must be in a BeginInit/EndInit block
            myBitmapImage.BeginInit();
            myBitmapImage.UriSource = item.Uri;
            myBitmapImage.CacheOption = BitmapCacheOption.None;

            // To save significant application memory, set the DecodePixelWidth or
            // DecodePixelHeight of the BitmapImage value of the image source to the desired
            // height or width of the rendered image. If you don't do this, the application will
            // cache the image as though it were rendered as its normal size rather than just
            // the size that is displayed.
            // Note: In order to preserve aspect ratio, set DecodePixelWidth
            // or DecodePixelHeight but not both.
            //myBitmapImage.DecodePixelWidth = 200;
            myBitmapImage.EndInit();
            _image.Source =myBitmapImage;

            MediaContainer.Children.Add(_image);
            MediaContainer.Visibility = Visibility.Visible;

            if (item.MediaType == MediaType.Image)
            {
                _timer.Start();
            }
        }

        private void VLCPlayer_EndReached(object sender, EventArgs e) => ThreadPool.QueueUserWorkItem(_ => DequeueAndPlay());

        private void DequeueAndPlay()
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                if (MediaContainer.Children.Count > 0)
                    MediaContainer.Children.RemoveAt(0);
                if (_currentIndex >= _videoCount)
                    _currentIndex = 0;
                var result = _playlist[_currentIndex];

                if (result.MediaType == MediaType.Video)
                {
                    StartVideo(result);
                }
                else
                {
                    DisplayImage(result);
                }
                _currentIndex++;
               
            }));
        }

        protected override void OnClosed(EventArgs e)
        {
            videoView.Dispose();
        }
    }

}
