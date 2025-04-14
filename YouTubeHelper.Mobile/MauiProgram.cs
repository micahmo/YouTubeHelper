using CommunityToolkit.Maui;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.LifecycleEvents;
using MongoDBHelpers;
using Plugin.LocalNotification;
using ServerStatusBot.Definitions.Api;

namespace YouTubeHelper.Mobile
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("ionicons.ttf");
                })
                .UseMauiCommunityToolkit()
                .UseLocalNotification()
                .ConfigureLifecycleEvents(events =>
                {
#if ANDROID
                    events.AddAndroid(android =>
                    {
                        android
                            .OnResume(async (activity) =>
                            {
                                for (int i = 0; i < 10; ++i)
                                {
                                    if (!string.IsNullOrEmpty(DatabaseEngine.ConnectionString))
                                    {
                                        try
                                        {
                                            await ServerApiClient.Instance.ReconnectAllGroups();
                                            break;
                                        }
                                        catch (Exception ex)
                                        {
                                            // Ignore or log for testing
                                        }

                                        await Task.Delay(TimeSpan.FromSeconds(1));
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                            });
                    });
#endif
                });

            AllowMultiLineTruncationOnAndroid();

            return builder.Build();
        }

        // https://github.com/hartez/MultilineTruncate
        static void AllowMultiLineTruncationOnAndroid()
        {
#if ANDROID
            static void UpdateMaxLines(ILabelHandler handler, ILabel label)
            {
                var textView = handler.PlatformView;
                if (label is Label controlsLabel && textView.Ellipsize == Android.Text.TextUtils.TruncateAt.End)
                {
                    textView.SetMaxLines(controlsLabel.MaxLines);
                }
            }

            LabelHandler.Mapper.AppendToMapping(
                nameof(Label.LineBreakMode), UpdateMaxLines);

            LabelHandler.Mapper.AppendToMapping(
                nameof(Label.MaxLines), UpdateMaxLines);
#endif

        }
    }
}