#!/usr/bin/env python3
import math
import os
import struct
import sys
import zlib


BASE_SIZE = 1024
SCALE = 3


def blend_pixel(pixels, width, x, y, color):
    if x < 0 or y < 0 or x >= width:
        return

    index = (y * width + x) * 4
    if index < 0 or index + 3 >= len(pixels):
        return

    sr, sg, sb, sa = color
    if sa >= 255:
        pixels[index:index + 4] = bytes((sr, sg, sb, 255))
        return

    da = pixels[index + 3]
    alpha = sa / 255.0
    inverse = 1.0 - alpha

    pixels[index] = int(sr * alpha + pixels[index] * inverse)
    pixels[index + 1] = int(sg * alpha + pixels[index + 1] * inverse)
    pixels[index + 2] = int(sb * alpha + pixels[index + 2] * inverse)
    pixels[index + 3] = min(255, int(sa + da * inverse))


def rounded_rect_contains(x, y, x0, y0, x1, y1, radius):
    if x < x0 or y < y0 or x >= x1 or y >= y1:
        return False

    cx = min(max(x, x0 + radius), x1 - radius)
    cy = min(max(y, y0 + radius), y1 - radius)
    return (x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius


def fill_rounded_rect(pixels, width, height, rect, radius, color):
    x0, y0, x1, y1 = [int(v) for v in rect]
    radius = int(radius)

    for y in range(max(0, y0), min(height, y1)):
        for x in range(max(0, x0), min(width, x1)):
            if rounded_rect_contains(x, y, x0, y0, x1, y1, radius):
                blend_pixel(pixels, width, x, y, color)


def fill_icon_background(pixels, width, height):
    x0 = 86 * SCALE
    y0 = 86 * SCALE
    x1 = 938 * SCALE
    y1 = 938 * SCALE
    radius = 210 * SCALE

    for y in range(y0, y1):
        for x in range(x0, x1):
            if not rounded_rect_contains(x, y, x0, y0, x1, y1, radius):
                continue

            gx = (x - x0) / max(1, x1 - x0)
            gy = (y - y0) / max(1, y1 - y0)
            mix = (gx + gy) / 2.0

            r = int(23 * (1 - mix) + 18 * mix)
            g = int(93 * (1 - mix) + 28 * mix)
            b = int(106 * (1 - mix) + 40 * mix)
            blend_pixel(pixels, width, x, y, (r, g, b, 255))


def draw_line(pixels, width, height, start, end, stroke_width, color):
    x1, y1 = start
    x2, y2 = end
    half = stroke_width / 2.0
    min_x = int(max(0, min(x1, x2) - half - 2))
    max_x = int(min(width, max(x1, x2) + half + 2))
    min_y = int(max(0, min(y1, y2) - half - 2))
    max_y = int(min(height, max(y1, y2) + half + 2))

    dx = x2 - x1
    dy = y2 - y1
    length_squared = dx * dx + dy * dy

    for y in range(min_y, max_y):
        for x in range(min_x, max_x):
            if length_squared == 0:
                distance = math.hypot(x - x1, y - y1)
            else:
                t = max(0.0, min(1.0, ((x - x1) * dx + (y - y1) * dy) / length_squared))
                px = x1 + t * dx
                py = y1 + t * dy
                distance = math.hypot(x - px, y - py)

            if distance <= half:
                blend_pixel(pixels, width, x, y, color)


def draw_icon():
    width = BASE_SIZE * SCALE
    height = BASE_SIZE * SCALE
    pixels = bytearray(width * height * 4)

    fill_icon_background(pixels, width, height)

    fill_rounded_rect(
        pixels,
        width,
        height,
        (302 * SCALE, 230 * SCALE, 746 * SCALE, 820 * SCALE),
        46 * SCALE,
        (0, 0, 0, 62),
    )
    fill_rounded_rect(
        pixels,
        width,
        height,
        (284 * SCALE, 208 * SCALE, 728 * SCALE, 798 * SCALE),
        46 * SCALE,
        (236, 242, 246, 255),
    )
    fill_rounded_rect(
        pixels,
        width,
        height,
        (284 * SCALE, 208 * SCALE, 374 * SCALE, 798 * SCALE),
        46 * SCALE,
        (44, 181, 205, 255),
    )
    fill_rounded_rect(
        pixels,
        width,
        height,
        (344 * SCALE, 286 * SCALE, 648 * SCALE, 334 * SCALE),
        16 * SCALE,
        (214, 224, 232, 255),
    )
    fill_rounded_rect(
        pixels,
        width,
        height,
        (430 * SCALE, 635 * SCALE, 636 * SCALE, 674 * SCALE),
        14 * SCALE,
        (185, 199, 209, 255),
    )
    fill_rounded_rect(
        pixels,
        width,
        height,
        (430 * SCALE, 704 * SCALE, 612 * SCALE, 743 * SCALE),
        14 * SCALE,
        (185, 199, 209, 255),
    )

    dark = (22, 35, 48, 255)
    draw_line(pixels, width, height, (420 * SCALE, 562 * SCALE), (420 * SCALE, 400 * SCALE), 54 * SCALE, dark)
    draw_line(pixels, width, height, (420 * SCALE, 400 * SCALE), (512 * SCALE, 504 * SCALE), 54 * SCALE, dark)
    draw_line(pixels, width, height, (512 * SCALE, 504 * SCALE), (604 * SCALE, 400 * SCALE), 54 * SCALE, dark)
    draw_line(pixels, width, height, (604 * SCALE, 400 * SCALE), (604 * SCALE, 562 * SCALE), 54 * SCALE, dark)

    return downsample(pixels, width, height, BASE_SIZE, BASE_SIZE)


def downsample(src, src_width, src_height, dst_width, dst_height):
    scale_x = src_width // dst_width
    scale_y = src_height // dst_height
    dst = bytearray(dst_width * dst_height * 4)

    for y in range(dst_height):
        for x in range(dst_width):
            total = [0, 0, 0, 0]

            for by in range(scale_y):
                for bx in range(scale_x):
                    src_index = ((y * scale_y + by) * src_width + (x * scale_x + bx)) * 4
                    total[0] += src[src_index]
                    total[1] += src[src_index + 1]
                    total[2] += src[src_index + 2]
                    total[3] += src[src_index + 3]

            count = scale_x * scale_y
            dst_index = (y * dst_width + x) * 4
            dst[dst_index] = total[0] // count
            dst[dst_index + 1] = total[1] // count
            dst[dst_index + 2] = total[2] // count
            dst[dst_index + 3] = total[3] // count

    return dst


def png_chunk(chunk_type, data):
    checksum = zlib.crc32(chunk_type + data) & 0xFFFFFFFF
    return struct.pack(">I", len(data)) + chunk_type + data + struct.pack(">I", checksum)


def make_png(width, height, pixels):
    raw = bytearray()

    for y in range(height):
        raw.append(0)
        row_start = y * width * 4
        raw.extend(pixels[row_start:row_start + width * 4])

    data = bytearray()
    data.extend(b"\x89PNG\r\n\x1a\n")
    data.extend(png_chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)))
    data.extend(png_chunk(b"IDAT", zlib.compress(bytes(raw), 9)))
    data.extend(png_chunk(b"IEND", b""))

    return bytes(data)


def write_png(path, width, height, pixels):
    data = make_png(width, height, pixels)

    with open(path, "wb") as file:
        file.write(data)


def write_icns(path, png_by_size):
    chunk_map = [
        (16, b"icp4"),
        (32, b"icp5"),
        (64, b"icp6"),
        (128, b"ic07"),
        (256, b"ic08"),
        (512, b"ic09"),
        (1024, b"ic10"),
    ]

    chunks = []
    for size, chunk_type in chunk_map:
        png = png_by_size[size]
        chunks.append(chunk_type + struct.pack(">I", len(png) + 8) + png)

    total_size = 8 + sum(len(chunk) for chunk in chunks)

    with open(path, "wb") as file:
        file.write(b"icns")
        file.write(struct.pack(">I", total_size))
        for chunk in chunks:
            file.write(chunk)


def main():
    if len(sys.argv) != 3:
        print("usage: generate_icon.py <AppIcon.iconset> <AppIcon.icns>", file=sys.stderr)
        return 2

    iconset_dir = sys.argv[1]
    icns_path = sys.argv[2]
    os.makedirs(iconset_dir, exist_ok=True)

    base = draw_icon()
    png_by_size = {}
    targets = [
        ("icon_16x16.png", 16),
        ("icon_16x16@2x.png", 32),
        ("icon_32x32.png", 32),
        ("icon_32x32@2x.png", 64),
        ("icon_128x128.png", 128),
        ("icon_128x128@2x.png", 256),
        ("icon_256x256.png", 256),
        ("icon_256x256@2x.png", 512),
        ("icon_512x512.png", 512),
        ("icon_512x512@2x.png", 1024),
    ]

    for filename, size in targets:
        pixels = base if size == BASE_SIZE else downsample(base, BASE_SIZE, BASE_SIZE, size, size)
        png_by_size[size] = make_png(size, size, pixels)
        write_png(os.path.join(iconset_dir, filename), size, size, pixels)

    write_icns(icns_path, png_by_size)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
