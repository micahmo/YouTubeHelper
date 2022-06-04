using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using YouTubeHelper.Models;
using YouTubeHelper.Utilities;

namespace YouTubeHelper.ViewModels
{
    public class ChannelViewModel : ObservableObject
    {
        public ChannelViewModel(Channel channel, MainControlViewModel mainControlViewModel)
        {
            Channel = channel;
            _mainControlViewModel = mainControlViewModel;
        }

        public ICommand DeleteCommand => _deleteChannelCommand ??= new RelayCommand(Delete);
        private ICommand _deleteChannelCommand;

        private void Delete()
        {
            _mainControlViewModel.SelectedChannel = _mainControlViewModel.Channels[Math.Max(0, _mainControlViewModel.Channels.IndexOf(this) - 1)];
            _mainControlViewModel.Channels.Remove(this);
            DatabaseEngine.ChannelCollection.Delete(Channel.ObjectId);
        }

        public ICommand LookupChannelCommand => _searchCommand ??= new RelayCommand(LookupChannel);
        private ICommand _searchCommand;

        private async void LookupChannel()
        {
            if (await YouTubeApi.Instance.PopulateChannel(Channel))
            {
                SearchGlyph = Icons.Check;
                await Task.Delay(TimeSpan.FromSeconds(5));
                SearchGlyph = Icons.Search;
            }
            else
            {
                SearchGlyph = Icons.X;
                await Task.Delay(TimeSpan.FromSeconds(5));
                SearchGlyph = Icons.Search;
            }
        }

        public ICommand FindVideosCommand => _findVideosCommand ??= new RelayCommand(FindVideos);
        private ICommand _findVideosCommand;

        private async void FindVideos()
        {
            _mainControlViewModel.IsBusy = true;

            Videos.Clear();

            try
            {
                (await YouTubeApi.Instance.FindVideos(Channel)).ToList().ForEach(v => Videos.Add(new VideoViewModel(v)));
            }
            finally
            {
                _mainControlViewModel.IsBusy = false;
            }
        }

        public string SearchGlyph
        {
            get => _searchGlyph;
            set => SetProperty(ref _searchGlyph, value);
        }
        private string _searchGlyph = Icons.Search;

        public ObservableCollection<VideoViewModel> Videos { get; } = new()
        {
            //new VideoViewModel(new Video { ThumbnailUrl = "https://i.ytimg.com/vi_webp/GBnGrMfqFao/maxresdefault.webp", Title = "Amber Heard's Attorney Speaks Out | xQc Reacts", Description = "Subscribe to my other Youtube channels for even more content! \nxQc Reacts: https://bit.ly/3FJk2Il\nxQc Gaming: https://bit.ly/3DGwBSF\nxQc Clips: https://bit.ly/3p3EFZC\nMain Channel: https://bit.ly/3glPvVC \n\nPlease subscribe, like and turn on notifications if you enjoyed the video!\nStreaming every day on Twitch! https://twitch.tv/xqc\n\nG-FUEL 'The Juice' ► USE CODE \"XQC\" FOR 30% OFF - https://gfuel.com/collections/the-juice\n\nStay Connected with xQc:\n►Twitter: https://twitter.com/xqc\n►Reddit: https://www.reddit.com/r/xqcow/\n►Discord: https://discord.gg/xqcow\n►Instagram: https://instagram.com/xqcow1/\n►Snapchat: xqcow1\n\n                        - WATCH MORE -\nAmong Us: https://www.youtube.com/playlist?list=PLKeR9CeyAc9as68PDcqQUGd7erKulABIU\nDaily Dose of Internet: https://www.youtube.com/playlist?list=PLKeR9CeyAc9ZH3cZR0QguDQ7_-2hgVowK\nJackbox Party Games: https://www.youtube.com/playlist?list=PLKeR9CeyAc9Z-qcdp7jq8UKCjNI0nRH1W\nViewer Picture Reviews: https://www.youtube.com/playlist?list=PLKeR9CeyAc9YlTaj3ENlo8d-oa65uSSiK\nMemes: https://www.youtube.com/playlist?list=PLKeR9CeyAc9aHzd9UHK6VncJwPuNZ4qqG\nReddit Recap: https://www.youtube.com/playlist?list=PLKeR9CeyAc9ZLiWPuqd4HU32W63O0n4H4\nJubilee: https://www.youtube.com/playlist?list=PLKeR9CeyAc9YPzqPwUJPzInpMgcUwdIWQ\n\nEdited by: Daily Dose of xQc\nIf you own copyrighted material in this video and would like it removed please contact me at one of the following:\n►https://twitter.com/DailyDoseofxQc\n►dailydoseofxqc@gmail.com\n\n#xQc #AmberHeard #JohnnyDepp", Duration = TimeSpan.FromMinutes(25.5), ReleaseDate = DateTimeOffset.Now - TimeSpan.FromDays(5) }),
            //new VideoViewModel(new Video { ThumbnailUrl = "https://i.ytimg.com/vi_webp/GBnGrMfqFao/maxresdefault.webp", Title = "Amber Heard's Attorney Speaks Out | xQc Reacts", Description = "Subscribe to my other Youtube channels for even more content! \nxQc Reacts: https://bit.ly/3FJk2Il\nxQc Gaming: https://bit.ly/3DGwBSF\nxQc Clips: https://bit.ly/3p3EFZC\nMain Channel: https://bit.ly/3glPvVC \n\nPlease subscribe, like and turn on notifications if you enjoyed the video!\nStreaming every day on Twitch! https://twitch.tv/xqc\n\nG-FUEL 'The Juice' ► USE CODE \"XQC\" FOR 30% OFF - https://gfuel.com/collections/the-juice\n\nStay Connected with xQc:\n►Twitter: https://twitter.com/xqc\n►Reddit: https://www.reddit.com/r/xqcow/\n►Discord: https://discord.gg/xqcow\n►Instagram: https://instagram.com/xqcow1/\n►Snapchat: xqcow1\n\n                        - WATCH MORE -\nAmong Us: https://www.youtube.com/playlist?list=PLKeR9CeyAc9as68PDcqQUGd7erKulABIU\nDaily Dose of Internet: https://www.youtube.com/playlist?list=PLKeR9CeyAc9ZH3cZR0QguDQ7_-2hgVowK\nJackbox Party Games: https://www.youtube.com/playlist?list=PLKeR9CeyAc9Z-qcdp7jq8UKCjNI0nRH1W\nViewer Picture Reviews: https://www.youtube.com/playlist?list=PLKeR9CeyAc9YlTaj3ENlo8d-oa65uSSiK\nMemes: https://www.youtube.com/playlist?list=PLKeR9CeyAc9aHzd9UHK6VncJwPuNZ4qqG\nReddit Recap: https://www.youtube.com/playlist?list=PLKeR9CeyAc9ZLiWPuqd4HU32W63O0n4H4\nJubilee: https://www.youtube.com/playlist?list=PLKeR9CeyAc9YPzqPwUJPzInpMgcUwdIWQ\n\nEdited by: Daily Dose of xQc\nIf you own copyrighted material in this video and would like it removed please contact me at one of the following:\n►https://twitter.com/DailyDoseofxQc\n►dailydoseofxqc@gmail.com\n\n#xQc #AmberHeard #JohnnyDepp", Duration = TimeSpan.FromMinutes(25.5), ReleaseDate = DateTimeOffset.Now - TimeSpan.FromDays(5) })
        };

        public Channel Channel { get; }

        private readonly MainControlViewModel _mainControlViewModel;
    }
}
