HARVEST PANIC - Grandpa's Camcorder
FARMER vs. THE RIVER, $−240

195 frames at 15 fps (960x540).
Make an MP4 (run inside this folder):
  ffmpeg -framerate 15 -i frames/frame_%04d.png -vf "scale=trunc(iw/2)*2:trunc(ih/2)*2" -c:v libx264 -pix_fmt yuv420p -movflags +faststart clip.mp4
Make a GIF:
  ffmpeg -framerate 15 -i frames/frame_%04d.png -vf "fps=15,scale=480:-1:flags=lanczos" clip.gif
