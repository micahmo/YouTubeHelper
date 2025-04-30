## Source

- Assets\
  - **logo_orig.svg**

## YouTubeHelper

- Images\
  - **logo.ico**
	- 16x16 - 256x25
	- Manually constructed in GIMP from PNG exported from SVG
	- Used as the `ApplicationIcon` in the project file

## YouTubeHelper.Mobile

- Resources\
  - AppIcon\
    - **logo.svg**
	- Used as the `MauiIcon` and the `MauiSplashScreen` in the project file
	- Exported fron original SVG (Page as Plain SVG)
  - Images\
    - **notification_icon.png**
	- 96x96
	- Exported from logo_orig.svg with full transparency in the middle (Path -> Difference)
	- Used as the `default_notification_icon` in the manifest, as the `SmallIcon` in the `AndroidNotificationHelper`, and as the small icon in `UpdateNotification()`