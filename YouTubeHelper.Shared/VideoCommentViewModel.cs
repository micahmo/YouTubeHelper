using ServerStatusBot.Definitions.Api;
using ServerStatusBot.Definitions.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace YouTubeHelper.Shared
{
    public class VideoCommentViewModel : INotifyPropertyChanged
    {
        private readonly VideoComment _comment;
        private readonly string? _videoId;
        private readonly List<VideoCommentViewModel> _preloadedReplies;
        private List<VideoCommentViewModel>? _loadedReplies;
        private bool _repliesLoaded;

        public VideoCommentViewModel(VideoComment comment, string? videoId = null)
        {
            _comment = comment;
            _videoId = videoId;
            _preloadedReplies = _comment.Replies.Select(r => new VideoCommentViewModel(r)).ToList();

            DisplayedReplies = new ObservableCollection<VideoCommentViewModel>(_preloadedReplies);
            DisplayedReplies.CollectionChanged += OnDisplayedRepliesChanged;
        }

        private void OnDisplayedRepliesChanged(object? sender, NotifyCollectionChangedEventArgs e)
            => OnPropertyChanged(nameof(HasDisplayedReplies));

        public string CommentId => _comment.CommentId;
        public string AuthorDisplayName => _comment.AuthorDisplayName;
        public string Text => _comment.Text;
        public long LikeCount => _comment.LikeCount;
        public bool IsCreatorComment => _comment.IsCreatorComment;
        public int TotalReplyCount => _comment.TotalReplyCount;

        public bool HasDisplayedReplies => DisplayedReplies.Count > 0;

        // Show toggle when there are more replies than the preloaded set, or when expanded (so user can collapse)
        public bool HasMoreReplies => TotalReplyCount > _preloadedReplies.Count || _repliesExpanded;

        public string ExpandRepliesText => _repliesExpanded
            ? "Hide replies"
            : $"View all {TotalReplyCount} {(TotalReplyCount == 1 ? "reply" : "replies")}";

        // Mutated in-place rather than replaced, so WPF ItemsControl updates incrementally (no scroll jump)
        public ObservableCollection<VideoCommentViewModel> DisplayedReplies { get; }

        private void SetDisplayedReplies(IEnumerable<VideoCommentViewModel> items)
        {
            DisplayedReplies.Clear();
            foreach (VideoCommentViewModel item in items)
                DisplayedReplies.Add(item);
        }

        private bool _isLoadingReplies;
        public bool IsLoadingReplies
        {
            get => _isLoadingReplies;
            private set { _isLoadingReplies = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotLoadingReplies)); }
        }
        public bool IsNotLoadingReplies => !_isLoadingReplies;

        private bool _repliesExpanded;
        public bool RepliesExpanded
        {
            get => _repliesExpanded;
            private set
            {
                _repliesExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasMoreReplies));
                OnPropertyChanged(nameof(ExpandRepliesText));
            }
        }

        public ICommand ToggleRepliesCommand => _toggleRepliesCommand ??= new SimpleRelayCommand(() => _ = ToggleReplies());
        private ICommand? _toggleRepliesCommand;

        private async Task ToggleReplies()
        {
            if (_repliesExpanded)
            {
                // Collapse back to preloaded notable replies
                SetDisplayedReplies(_preloadedReplies);
                RepliesExpanded = false;
                return;
            }

            if (_repliesLoaded)
            {
                // Restore the previously loaded full list and re-expand
                SetDisplayedReplies(_loadedReplies!);
                RepliesExpanded = true;
                return;
            }

            // Capture UI context before the await so we can marshal back for MAUI
            SynchronizationContext? uiContext = SynchronizationContext.Current;

            IsLoadingReplies = true;

            List<VideoComment> allReplies = await ServerApiClient.Instance
                .GetCommentReplies(CommentId, _videoId)
                .ConfigureAwait(false);

            List<VideoCommentViewModel> allRepliesVm = allReplies
                .Select(r => new VideoCommentViewModel(r))
                .ToList();

            _loadedReplies = allRepliesVm;
            _repliesLoaded = true;

            void applyUpdates()
            {
                SetDisplayedReplies(allRepliesVm);
                IsLoadingReplies = false;
                RepliesExpanded = true;
            }

            if (uiContext != null && uiContext != SynchronizationContext.Current)
                uiContext.Post(_ => applyUpdates(), null);
            else
                applyUpdates();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    internal class SimpleRelayCommand : ICommand
    {
        private readonly Action _execute;
        public SimpleRelayCommand(Action execute) => _execute = execute;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }
}
