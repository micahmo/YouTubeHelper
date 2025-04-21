using CommunityToolkit.Maui;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.LifecycleEvents;
using MongoDBHelpers;
using Plugin.LocalNotification;
using ServerStatusBot.Definitions.Api;

#if ANDROID
using Plugin.Firebase.Core.Platforms.Android;
#endif

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
                        android.OnPause(_ =>
                        {
                            LocalNotificationCenter.Current.CancelAll();
                        });

                        android.OnStop(_ =>
                        {
                            LocalNotificationCenter.Current.CancelAll();
                        });

                        android.OnStart(_ =>
                        {
                            LocalNotificationCenter.Current.CancelAll();
                        });

                        android.OnResume(async _ =>
                        {
                            LocalNotificationCenter.Current.CancelAll();

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
                                        Console.WriteLine($"Error reconnecting to SignalR group: {ex}");
                                    }

                                    await Task.Delay(TimeSpan.FromSeconds(1));
                                }
                                else
                                {
                                    break;
                                }
                            }
                        });

                        android.OnCreate((activity, _) =>
                            CrossFirebase.Initialize(activity));
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
