using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using ChillPlayer.Models;
using ChillPlayer.Web;
using Newtonsoft.Json.Linq;
using Octane.Xamarin.Forms.VideoPlayer;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

// http://www.genyoutube.net/formats-resolution-youtube-videos.html

namespace ChillPlayer.MarkupExtensions
{
    /// <summary>
    /// Converts a YouTube video ID into a direct URL that is playable by the Xamarin Forms VideoPlayer.
    /// </summary>
    [ContentProperty("VideoId")]
    public class YouTubeVideoIdExtension : IMarkupExtension
    {
        #region Properties

        /// <summary>
        /// The video identifier associated with the video stream.
        /// </summary>
        /// <value>
        /// The video identifier.
        /// </value>
        public string VideoId { get; set; }

        #endregion

        #region IMarkupExtension

        /// <summary>
        /// Provides the value.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <returns></returns>
        public object ProvideValue(IServiceProvider serviceProvider)
        {
            try
            {
                Debug.WriteLine($"Acquiring YouTube stream source URL from VideoId='{VideoId}'...");
                var videoInfoUrl = $"http://www.youtube.com/get_video_info?video_id={VideoId}";

                using (var client = new HttpClient())
                {
                    var videoPageContent = client.GetStringAsync(videoInfoUrl).Result;
                    var videoParameters = HttpUtility.ParseQueryString(videoPageContent);

                    List<YoutubeStream> streams;
                    var encodedStreamsDelimited = WebUtility.HtmlDecode(videoParameters["url_encoded_fmt_stream_map"]);
                    if (!string.IsNullOrEmpty(encodedStreamsDelimited))
                    {
                        var encodedStreams = encodedStreamsDelimited.Split(',');
                        var encodedStreamsParsed = encodedStreams.Select(HttpUtility.ParseQueryString);
                        streams = encodedStreamsParsed.Select(x => new YoutubeStream()
                        {
                            mimeType = x["type"],
                            quality = x["quality"],
                            url = x["url"]
                        }).ToList();
                    }
                    else
                    {
                        var playerResponse = WebUtility.HtmlDecode(videoParameters["player_response"]);
                        var playerResponseObject = JObject.Parse(playerResponse);
                        var streamsList = playerResponseObject["streamingData"]["formats"].ToObject<YoutubeStream[]>();
                        var adaptiveStreamsList = playerResponseObject["streamingData"]["adaptiveFormats"].ToObject<YoutubeStream[]>();
                        streams = streamsList.Union(adaptiveStreamsList).ToList();
                    }

                    var streamsByPriority = streams
                        .OrderBy(s =>
                        {
                            var type = s.mimeType;
                            if (type.Contains("video/mp4")) return 10;
                            if (type.Contains("video/3gpp")) return 20;
                            if (type.Contains("video/x-flv")) return 30;
                            if (type.Contains("video/webm")) return 40;
                            return int.MaxValue;
                        })
                        .ThenBy(s =>
                        {
                            var quality = s.quality;
                            return Array.IndexOf(new[] { "medium", "high", "small" }, quality);
                        })
                        .FirstOrDefault();

                    Debug.WriteLine($"Stream URL: {streamsByPriority.url}");
                    return VideoSource.FromUri(streamsByPriority.url);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error occured while attempting to convert YouTube video ID into a remote stream path.");
                Debug.WriteLine(ex);
            }

            return null;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Convert the specified video ID into a streamable YouTube URL.
        /// </summary>
        /// <param name="videoId">Video identifier.</param>
        /// <returns></returns>
        public static VideoSource Convert(string videoId)
        {
            var markupExtension = new YouTubeVideoIdExtension { VideoId = videoId };
            return (VideoSource)markupExtension.ProvideValue(null);
        }

        #endregion
    }
}
