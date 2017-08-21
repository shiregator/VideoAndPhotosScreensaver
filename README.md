# Photo and Video Screensaver
A Windows screensaver that plays photos and videos (sound is configurable).

## Shortcut Keys
          Up Arrow - Volume up
          Down Arrow - Volume down
          0 - Mute volume
          Right arrow - next image/video
          Left arrow - previous image/video
          P - Pause/unpause
          Delete - Delete current file
          I - Show info overlay
          H - Show this message
          R - Rotate image
          O - Open file
          

## Setup
1. Copy the VideoScreensaver.scr file into your C:\Windows directory
2. Open the Screen Saver Settings (Search on "lock screen" -> open "Screen saver settings"
3. Select "VideoScreensaver"
4. Click "Settings..."
5. Click "Add new folder" to include your pictures and videos folder (can add multiple)
6. Select preferred algorithm

## Screensaver Settings / Configuration
1. Interval in ms: Time between transitions
2. Video volume timeout in min (0 to mute): If video volume is enabled it will be muted after this number of minutes
3. Volume: Video playback volume
4. Media folders: Folders containing pictures and videos. Multiple folders can be added
5. Media change algorithm: Algorithm for transitioning through pictures
6. Remove all settings: This will remove all settings from the registry

## Acknowledgements
This project is active thanks to the effort of [Michael Barnathan](https://sourceforge.net/u/metasquares/profile/) and Chris Lott.
I forked it from https://github.com/chrislott/Videosaver who copied it from SourceForge(https://sourceforge.net/projects/videosaver/).  Also special thanks to @sergeiwork for his efforts on the recent enhancements.

## Requirements
This C# project was created with Visual Studio Community 2017, and tested with Windows 10.

