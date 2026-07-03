#!/usr/bin/env python3
import os
import struct
import sys


def main():
    if len(sys.argv) != 3:
        print("usage: generate_windows_icon.py <input.png> <output.ico>", file=sys.stderr)
        return 2

    png_path = sys.argv[1]
    ico_path = sys.argv[2]

    with open(png_path, "rb") as file:
        png_data = file.read()

    os.makedirs(os.path.dirname(ico_path), exist_ok=True)

    with open(ico_path, "wb") as file:
        file.write(struct.pack("<HHH", 0, 1, 1))
        file.write(struct.pack("<BBBBHHII", 0, 0, 0, 0, 1, 32, len(png_data), 22))
        file.write(png_data)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
