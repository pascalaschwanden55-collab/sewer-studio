import os
import xml.etree.ElementTree as ET

# Pfade anpassen
M150_PATH = r"E:\Wassen_Projekte\Wassen\2014\Dorf Wassen\Projects\Gem. Wassen TV Untersuchung 2014\PDF\22_05_2014__M150_Export.xml"
VIDEO_DIR = r"E:\Wassen_Projekte\Wassen\2014\Dorf Wassen\Projects\Gem. Wassen TV Untersuchung 2014\Video"
FOTO_DIR = r"E:\Wassen_Projekte\Wassen\2014\Dorf Wassen\Projects\Gem. Wassen TV Untersuchung 2014\Foto"

# Alle Videodateien
video_files = set(f for f in os.listdir(VIDEO_DIR) if f.lower().endswith(('.mp2','.mp4','.avi','.mov','.wmv','.mkv')))
# Alle Fotodateien
foto_files = set(f for f in os.listdir(FOTO_DIR) if f.lower().endswith(('.jpg','.jpeg','.png','.bmp','.tif','.tiff')))

# XML parsen
root = ET.parse(M150_PATH).getroot()

missing_videos = []
missing_photos = []

for hi in root.findall('.//HI'):
    # Video
    hi116 = hi.findtext('HI116')
    if hi116 and hi116.strip():
        if hi116.strip() not in video_files:
            missing_videos.append(hi116.strip())
    # Fotos
    for hz in hi.findall('HZ'):
        hz009 = hz.findtext('HZ009')
        if hz009 and hz009.strip():
            if hz009.strip() not in foto_files:
                missing_photos.append(hz009.strip())

print("Fehlende Videos:")
for v in sorted(set(missing_videos)):
    print(v)
print("\nFehlende Fotos:")
for f in sorted(set(missing_photos)):
    print(f)
