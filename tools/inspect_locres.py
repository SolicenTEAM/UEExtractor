#!/usr/bin/env python3
"""
Diagnoses a .locres file: shows header bytes, interprets both standard-compact
and NTE-compact layouts, validates the stored string-table offset.

Usage:
    python inspect_locres.py <path/to/Game.locres>
"""
import struct, sys, os

MAGIC = bytes([0x0E, 0x14, 0x74, 0x75, 0x67, 0x4A, 0x03, 0xFC,
               0x4A, 0x15, 0x90, 0x9D, 0xC3, 0x37, 0x7F, 0x1B])

def read_fstring(data, pos):
    """Returns (string_value, next_pos). Handles ASCII (positive len) and UTF-16LE (negative len)."""
    if pos + 4 > len(data):
        return None, pos
    length = struct.unpack_from('<i', data, pos)[0]
    pos += 4
    if length == 0:
        return '', pos
    if length > 0:
        raw = data[pos:pos + length - 1]
        pos += length
        return raw.decode('ascii', errors='replace'), pos
    else:
        char_count = (-length) - 1
        raw = data[pos:pos + char_count * 2]
        pos += (-length) * 2
        return raw.decode('utf-16-le', errors='replace'), pos

def read_key_fstring(data, pos):
    """Same encoding as read_fstring (used for key section)."""
    return read_fstring(data, pos)

def hex_bytes(data, pos, n):
    chunk = data[pos:pos+n]
    return ' '.join(f'{b:02X}' for b in chunk)

def main():
    if len(sys.argv) < 2:
        print("Usage: python inspect_locres.py <file.locres>")
        sys.exit(1)

    path = sys.argv[1]
    with open(path, 'rb') as f:
        data = f.read()

    size = len(data)
    print(f"\n{'='*60}")
    print(f"File: {os.path.basename(path)}  ({size:,} bytes)")
    print(f"{'='*60}\n")

    # --- Magic ---
    magic_ok = data[:16] == MAGIC
    print(f"[0x00] Magic      : {'OK' if magic_ok else 'MISMATCH'}")
    print(f"       Raw        : {hex_bytes(data, 0, 16)}")

    # --- Version ---
    version = data[16]
    version_names = {0: 'Legacy', 1: 'Compact', 2: 'Optimized_CRC32', 3: 'Optimized_CityHash64'}
    print(f"[0x10] UE version : {version} ({version_names.get(version, 'Unknown')})")

    # ----------------------------------------------------------------
    # STANDARD COMPACT layout  (our writer)
    #   pos 17: int64 stringTableOffset
    #   pos 25: int32 namespaceCount  (key section starts here)
    # ----------------------------------------------------------------
    print(f"\n{'─'*60}")
    print("INTERPRETATION A – Standard Compact (our current writer)")
    print(f"{'─'*60}")
    std_offset = struct.unpack_from('<q', data, 17)[0]
    print(f"[0x11] String table offset (int64) = {std_offset:,}  (0x{std_offset:X})")
    ns_count_std = struct.unpack_from('<i', data, 25)[0] if size >= 29 else -1
    print(f"[0x19] Namespace count @ pos 25 = {ns_count_std}")

    # Peek at what's stored at std_offset
    if 0 <= std_offset < size:
        str_count_std = struct.unpack_from('<I', data, std_offset)[0]
        print(f"       String count at offset   = {str_count_std:,}")
        p = std_offset + 4
        for idx in range(min(3, str_count_std)):
            s, p = read_fstring(data, p)
            if s is None: break
            print(f"       String[{idx}] = {repr(s[:80])}")
    else:
        print(f"       Offset {std_offset:,} is OUT OF FILE (file is {size:,} bytes)  ← WRONG OFFSET")

    # ----------------------------------------------------------------
    # NTE COMPACT layout
    #   pos 17: int32 nte_version
    #   pos 21: bool isEncrypted  (only if nte_version >= 10100)
    #           OR no bool if nte_version in [10000,10099]
    #           OR no bool if nte_version < 10000
    #   then  : int64 stringTableOffset
    # ----------------------------------------------------------------
    print(f"\n{'─'*60}")
    print("INTERPRETATION B – NTE Compact (game's custom reader)")
    print(f"{'─'*60}")
    nte_version = struct.unpack_from('<i', data, 17)[0]
    print(f"[0x11] NTE version (int32) @ 17 = {nte_version}")

    if nte_version >= 10100:
        is_encrypted = bool(data[21])
        nte_offset_pos = 22
        print(f"[0x15] isEncrypted (bool) @ 21  = {is_encrypted}")
    elif nte_version >= 10000:
        is_encrypted = True
        nte_offset_pos = 21
        print(f"       isEncrypted               = True (implicit, version 10000-10099)")
    else:
        is_encrypted = False
        nte_offset_pos = 21
        print(f"       isEncrypted               = False (version < 10000, unencrypted)")

    nte_key_section_start = nte_offset_pos + 8  # right after the int64 offset field

    if nte_offset_pos + 8 <= size:
        nte_offset = struct.unpack_from('<q', data, nte_offset_pos)[0]
        print(f"[0x{nte_offset_pos:02X}] String table offset (int64) = {nte_offset:,}  (0x{nte_offset:X})")
        ns_count_nte = struct.unpack_from('<i', data, nte_key_section_start)[0] if size >= nte_key_section_start + 4 else -1
        print(f"[0x{nte_key_section_start:02X}] Namespace count @ pos {nte_key_section_start} = {ns_count_nte}")

        if 0 <= nte_offset < size:
            str_count_nte = struct.unpack_from('<I', data, nte_offset)[0]
            print(f"       String count at NTE offset = {str_count_nte:,}")
            p = nte_offset + 4
            for idx in range(min(30, str_count_nte)):
                s, p = read_fstring(data, p)
                if s is None: break
                print(f"       String[{idx}] = {repr(s[:80])}")
        else:
            print(f"       NTE offset {nte_offset:,} is OUT OF FILE  ← WRONG OFFSET")
    else:
        print("       Not enough bytes to read NTE offset")

    # ----------------------------------------------------------------
    # RAW BYTES at key positions
    # ----------------------------------------------------------------
    print(f"\n{'─'*60}")
    print("RAW BYTES at critical positions")
    print(f"{'─'*60}")
    for label, pos, n in [
        ("Pos  17-24 (std offset / NTE ver+bool+off)", 17, 16),
        ("Pos  25-32 (std ns_count / NTE offset)",     25, 8),
        ("Pos  45-56 (around error position 49)",      45, 16),
    ]:
        if pos < size:
            print(f"  {label}")
            print(f"    HEX: {hex_bytes(data, pos, min(n, size-pos))}")

    # ----------------------------------------------------------------
    # WALK the key section – try BOTH layouts, report which matches
    # ----------------------------------------------------------------
    for layout_name, walk_start in [("Standard (pos 25)", 25), ("NTE (pos 29)", 29)]:
        print(f"\n{'─'*60}")
        print(f"KEY SECTION WALK – {layout_name}")
        print(f"{'─'*60}")
        pos = walk_start
        if size < pos + 4:
            print("  Not enough data")
            continue
        ns_count = struct.unpack_from('<i', data, pos)[0]
        pos += 4
        if ns_count < 0 or ns_count > 100000:
            print(f"  Namespace count = {ns_count}  ← implausible, wrong layout?")
            continue
        print(f"  Namespace count: {ns_count}")
        walked_ok = True
        total_keys = 0
        for i in range(min(ns_count, 5)):
            ns, pos = read_key_fstring(data, pos)
            if ns is None:
                print(f"  [NS {i}] READ ERROR at pos {pos}")
                walked_ok = False; break
            kc = struct.unpack_from('<i', data, pos)[0]; pos += 4
            total_keys += kc
            print(f"  [NS {i}] '{ns}'  keys={kc}")
            for j in range(min(kc, 2)):
                k, pos = read_key_fstring(data, pos)
                pos += 8  # hash + strIdx
                if k is None:
                    walked_ok = False; break
                print(f"    [Key {j}] '{k}'")
            if not walked_ok: break
            if kc > 2:
                for j in range(kc - 2):
                    _, pos = read_key_fstring(data, pos)
                    pos += 8
        if walked_ok and ns_count > 5:
            for i in range(5, ns_count):
                _, pos = read_key_fstring(data, pos)
                kc = struct.unpack_from('<i', data, pos)[0]; pos += 4
                total_keys += kc
                for j in range(kc):
                    _, pos = read_key_fstring(data, pos)
                    pos += 8
        if walked_ok:
            ref_offset = std_offset if walk_start == 25 else nte_offset
            print(f"\n  After walking ALL {ns_count} namespaces ({total_keys} keys total):")
            print(f"  Actual key section end pos = {pos}")
            print(f"  Stored stringTableOffset   = {ref_offset:,}")
            if pos == ref_offset:
                print(f"  ✓ Offset matches – this layout is CORRECT")
            else:
                diff = ref_offset - pos
                print(f"  ✗ MISMATCH: offset is {abs(diff)} bytes {'too large' if diff > 0 else 'too small'}")
    print()

if __name__ == '__main__':
    main()
