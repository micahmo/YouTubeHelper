using CommunityToolkit.Maui;

namespace YouTubeHelper.Mobile
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder.UseMauiApp<App>().ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            }).UseMauiCommunityToolkit();

            AllowMultiLineTruncationOnAndroid();

            return builder.Build();
        }

        // https://github.com/hartez/MultilineTruncate
        static void AllowMultiLineTruncationOnAndroid()
        {
#if ANDROID
            static void UpdateMaxLines(Microsoft.Maui.Handlers.LabelHandler handler, ILabel label)
            {
                var textView = handler.PlatformView;
                if (label is Label controlsLabel && textView.Ellipsize == Android.Text.TextUtils.TruncateAt.End)
                {
                    textView.SetMaxLines(controlsLabel.MaxLines);
                }
            }

            Label.ControlsLabelMapper.AppendToMapping(
                nameof(Label.LineBreakMode), UpdateMaxLines);

            Label.ControlsLabelMapper.AppendToMapping(
                nameof(Label.MaxLines), UpdateMaxLines);
#endif

        }
    }
}