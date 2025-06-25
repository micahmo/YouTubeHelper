using Bluegrams.Application;
using HarmonyLib;
using Microsoft.Win32;
using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace YouTubeHelper.Utilities
{
    internal static class Versioning
    {
        public static string? GetInstalledMsiVersion()
        {
            const string guid = "{744FA957-AB5E-455A-8CEC-A29448D1FB93}";

            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using RegistryKey? key = baseKey.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{guid}_is1");
                return key?.GetValue("DisplayVersion") as string;
            }

            return null;
        }
    }

    [HarmonyPatch(typeof(AppInfo), "Version", MethodType.Getter)]
    public class VersionPatch
    {
        static bool Prefix(ref string __result)
        {
            __result = Versioning.GetInstalledMsiVersion() ?? Assembly.GetEntryAssembly()!.GetName().Version!.ToString();
            return false;
        }
    }

    [HarmonyPatch(typeof(Bluegrams.Application.WPF.UpdateWindow), MethodType.Constructor, typeof(bool), typeof(AppUpdate), typeof(bool))]
    class UpdateWindowPatch
    {
        static void Postfix(Window __instance)
        {
            __instance.Activated += (_, _) =>
            {
                __instance.Width = 500;
                __instance.Height = Double.NaN;
                __instance.SizeToContent = SizeToContent.Height;

                if (__instance.FindName("txtReleaseNotes") is TextBox notes)
                {
                    notes.TextWrapping = TextWrapping.Wrap;
                    notes.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                    notes.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                }

                __instance.InvalidateMeasure();
                __instance.InvalidateArrange();
                __instance.UpdateLayout();
            };
        }
    }
}
