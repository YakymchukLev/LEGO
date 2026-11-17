from PIL import Image
import os
import uuid

src = r"C:\Users\leoya\.cursor\projects\c-FORK-LEGO\assets\image-0a24f479-b2ab-451f-b2ff-b0fe1346795d.png"
out_dir = r"c:\FORK\LEGO\Assets\Resources\Sprites"
os.makedirs(out_dir, exist_ok=True)
out = os.path.join(out_dir, "freeze_snowflake_badge.png")
meta_out = out + ".meta"

img = Image.open(src).convert("RGBA")
w, h = img.size
pixels = img.load()


def is_red_bg(r, g, b):
    return (r > 80 and r > g * 1.35 and r > b * 1.35) or (r > 100 and g < 70 and b < 70)


xs, ys = [], []
for y in range(h):
    for x in range(w):
        r, g, b, a = pixels[x, y]
        if not is_red_bg(r, g, b):
            xs.append(x)
            ys.append(y)

minx, maxx = min(xs), max(xs)
miny, maxy = min(ys), max(ys)
pad = 8
minx = max(0, minx - pad)
miny = max(0, miny - pad)
maxx = min(w - 1, maxx + pad)
maxy = min(h - 1, maxy + pad)
print("bbox", minx, miny, maxx, maxy)

crop = img.crop((minx, miny, maxx + 1, maxy + 1))
cw, ch = crop.size
cp = crop.load()

# Deep cobalt / royal blue — clearly darker & more saturated than pale cyan ice
deep = (12, 55, 175)
mid = (36, 105, 230)
light = (95, 165, 255)
white = (210, 230, 255)

lums = []
for y in range(ch):
    for x in range(cw):
        r, g, b, a = cp[x, y]
        if is_red_bg(r, g, b):
            cp[x, y] = (0, 0, 0, 0)
        else:
            lum = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0
            lums.append(lum)

lo, hi = min(lums), max(lums)
print("lum range", lo, hi)

for y in range(ch):
    for x in range(cw):
        r, g, b, a = cp[x, y]
        if a == 0:
            continue
        if is_red_bg(r, g, b) or (r > 90 and g < 100 and b < 100 and r > g and r > b):
            cp[x, y] = (0, 0, 0, 0)
            continue

        lum = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0
        t = 0.0 if hi <= lo else (lum - lo) / (hi - lo)
        if t < 0.35:
            u = t / 0.35
            col = tuple(int(deep[i] * (1 - u) + mid[i] * u) for i in range(3))
        elif t < 0.72:
            u = (t - 0.35) / 0.37
            col = tuple(int(mid[i] * (1 - u) + light[i] * u) for i in range(3))
        else:
            u = (t - 0.72) / 0.28
            col = tuple(int(light[i] * (1 - u) + white[i] * u) for i in range(3))
        cp[x, y] = (col[0], col[1], col[2], 255)

side = max(cw, ch)
size = 256
canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
scale = (size * 0.92) / side
nw, nh = max(1, int(cw * scale)), max(1, int(ch * scale))
resized = crop.resize((nw, nh), Image.Resampling.LANCZOS)
ox = (size - nw) // 2
oy = (size - nh) // 2
canvas.paste(resized, (ox, oy), resized)

px = canvas.load()
for y in range(size):
    for x in range(size):
        r, g, b, a = px[x, y]
        if a == 0:
            continue
        if is_red_bg(r, g, b) or a < 40:
            px[x, y] = (0, 0, 0, 0)

canvas.save(out, "PNG")
print("saved", out, canvas.size)

guid = uuid.uuid4().hex
meta = f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 256
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 256
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: Standalone
    maxTextureSize: 256
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: Android
    maxTextureSize: 256
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: {guid[:16]}000000000000000000000000
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
with open(meta_out, "w", encoding="utf-8", newline="\n") as f:
    f.write(meta)
print("meta", meta_out)
