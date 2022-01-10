# timelapse
A set of tools/scripts to automate the taking and creation of timelapse videos and videos with a Raspberry Pi

# Setup



# On board timelapse generation

ffmpeg -r 30 -f image2 -pattern_type glob -i "./<YYYY-MM-DD>*.jpg" -s 1014x760 -vcodec libx264 <YYYY-MM-DD>.mp4
