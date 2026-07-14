#!/usr/bin/env python3
"""Re-tag the arm64 slice of a Unity native plugin static lib from platform iOS -> iOS-simulator.

The Google Mobile Ads Unity plugin ships a prebuilt `unity-plugin-library.a` whose arm64 objects
are marked LC_BUILD_VERSION platform = IOS (device). Linking an arm64 *simulator* app then fails
("built for 'iOS'"). arm64-device and arm64-simulator are the same machine code, so we just flip the
platform field (IOS=2 -> IOSSIMULATOR=7) in each object's LC_BUILD_VERSION. Keeps the x86_64 slice.
"""
import os, struct, subprocess, sys, tempfile, shutil

LC_BUILD_VERSION = 0x32
PLATFORM_IOS = 2
PLATFORM_IOS_SIM = 7
MH_MAGIC_64 = 0xFEEDFACF


def patch_macho(path):
    data = bytearray(open(path, "rb").read())
    if len(data) < 32 or struct.unpack("<I", data[0:4])[0] != MH_MAGIC_64:
        return False
    ncmds = struct.unpack("<I", data[16:20])[0]
    off, changed = 32, False
    for _ in range(ncmds):
        if off + 8 > len(data):
            break
        cmd, cmdsize = struct.unpack("<II", data[off:off + 8])
        if cmd == LC_BUILD_VERSION:
            plat = struct.unpack("<I", data[off + 8:off + 12])[0]
            if plat == PLATFORM_IOS:
                data[off + 8:off + 12] = struct.pack("<I", PLATFORM_IOS_SIM)
                changed = True
        off += cmdsize
    if changed:
        open(path, "wb").write(data)
    return changed


def fix_lib(lib):
    archs = subprocess.check_output(["lipo", "-info", lib], text=True)
    if "arm64" not in archs:
        return
    work = tempfile.mkdtemp()
    try:
        arm = os.path.join(work, "arm64.a")
        subprocess.run(["lipo", lib, "-thin", "arm64", "-output", arm], check=True)
        objdir = os.path.join(work, "obj")
        os.makedirs(objdir)
        subprocess.run(["ar", "x", arm], cwd=objdir, check=True)
        n = 0
        for f in os.listdir(objdir):
            if not f.endswith(".o"):
                continue
            try:
                if patch_macho(os.path.join(objdir, f)):
                    n += 1
            except OSError:
                pass
        # re-archive the patched arm64 objects
        arm_fixed = os.path.join(work, "arm64_sim.a")
        subprocess.run("ar crs " + arm_fixed + " " + os.path.join(objdir, "*.o"),
                       shell=True, check=True)
        # rebuild the fat lib: keep any non-arm64 slices, swap in the patched arm64
        parts = [arm_fixed]
        for other in ("x86_64",):
            if other in archs:
                p = os.path.join(work, other + ".a")
                subprocess.run(["lipo", lib, "-thin", other, "-output", p], check=True)
                parts.append(p)
        subprocess.run(["lipo", "-create", *parts, "-output", lib], check=True)
        print(f"  patched {os.path.basename(lib)}: {n} arm64 objects -> ios-simulator")
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    for lib in sys.argv[1:]:
        if os.path.exists(lib):
            fix_lib(lib)
        else:
            print(f"  (skip, missing) {lib}")
