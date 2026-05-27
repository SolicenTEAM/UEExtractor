#!/usr/bin/env python3

# Inspect locres file python tool from Nuked88 Fork: https://github.com/Nuked88/UEExtractor
# It has been retained as a useful diagnostic tool, all rights reserved for their creator - Nuked88 
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

def hex_bytes(data, pos, n):
    chunk = data[pos:pos+n]
    return ' '.join(f'{b:02X}' for b in chunk)

def walk_key_section_v1(data, pos, stored_offset, label):
    """Walk standard v1 key section: NamespaceCount + [FString + KeyCount + [FString + srcHash + strIdx]]"""
    print(f"\n{'─'*60}")
    print(f"KEY SECTION WALK – {label}")
    print(f"{'─'*60}")
    size = len(data)
    if size < pos + 4:
        print("  Not enough data"); return
    ns_count = struct.unpack_from('<I', data, pos)[0]; pos += 4
    if ns_count > 100000:
        print(f"  NamespaceCount = {ns_count}  ← implausible, wrong layout?"); return
    print(f"  NamespaceCount: {ns_count}")
    total_keys = 0; walked_ok = True
    for i in range(ns_count):
        ns, pos = read_fstring(data, pos)
        if ns is None: print(f"  [NS {i}] READ ERROR"); walked_ok = False; break
        kc = struct.unpack_from('<I', data, pos)[0]; pos += 4
        total_keys += kc
        if i < 5: print(f"  [NS {i}] '{ns}'  keys={kc}")
        for j in range(kc):
            k, pos = read_fstring(data, pos)
            if k is None: walked_ok = False; break
            pos += 8  # sourceHash(4) + strIdx(4)
            if i < 5 and j < 2: print(f"    [Key {j}] '{k}'")
        if not walked_ok: break
    if walked_ok:
        print(f"\n  After walking ALL {ns_count} namespaces ({total_keys} keys total):")
        print(f"  Actual key section end pos = {pos}")
        print(f"  Stored stringTableOffset   = {stored_offset:,}")
        if pos == stored_offset:
            print(f"  ✓ Offset matches")
        else:
            diff = stored_offset - pos
            print(f"  ✗ MISMATCH: offset is {abs(diff)} bytes {'too large' if diff > 0 else 'too small'}")

def walk_key_section_v3(data, pos, stored_offset, label):
    """Walk v3 key section: EntriesCount + NamespaceCount + [StrHash + FString + KeyCount + [StrHash + FString + srcHash + strIdx]]"""
    print(f"\n{'─'*60}")
    print(f"KEY SECTION WALK – {label} (v3/CityHash64)")
    print(f"{'─'*60}")
    size = len(data)
    if size < pos + 8:
        print("  Not enough data"); return
    entries_count = struct.unpack_from('<I', data, pos)[0]; pos += 4
    ns_count      = struct.unpack_from('<I', data, pos)[0]; pos += 4
    print(f"  EntriesCount (total keys): {entries_count}")
    if ns_count > 100000:
        print(f"  NamespaceCount = {ns_count}  ← implausible, wrong layout?"); return
    print(f"  NamespaceCount: {ns_count}")
    total_keys = 0; walked_ok = True
    for i in range(ns_count):
        ns_hash = struct.unpack_from('<I', data, pos)[0]; pos += 4  # CityHash64 low32
        ns, pos = read_fstring(data, pos)
        if ns is None: print(f"  [NS {i}] READ ERROR"); walked_ok = False; break
        kc = struct.unpack_from('<I', data, pos)[0]; pos += 4
        total_keys += kc
        if i < 5: print(f"  [NS {i}] hash=0x{ns_hash:08X} '{ns}'  keys={kc}")
        for j in range(kc):
            k_hash = struct.unpack_from('<I', data, pos)[0]; pos += 4  # CityHash64 low32
            k, pos = read_fstring(data, pos)
            if k is None: walked_ok = False; break
            pos += 8  # sourceHash(4) + strIdx(4)
            if i < 5 and j < 2: print(f"    [Key {j}] hash=0x{k_hash:08X} '{k}'")
        if not walked_ok: break
    if walked_ok:
        print(f"\n  After walking ALL {ns_count} namespaces ({total_keys} keys total):")
        print(f"  Actual key section end pos = {pos}")
        print(f"  Stored stringTableOffset   = {stored_offset:,}")
        if pos == stored_offset:
            print(f"  ✓ Offset matches – key section is CORRECT")
        else:
            diff = stored_offset - pos
            print(f"  ✗ MISMATCH: offset is {abs(diff)} bytes {'too large' if diff > 0 else 'too small'}")

def print_string_table(data, offset, count, has_refcount, n_show=10):
    """Print first n_show strings from the string table, skipping RefCount if present."""
    p = offset + 4  # skip the count uint32
    shown = 0
    for i in range(count):
        s, p = read_fstring(data, p)
        if s is None: break
        if has_refcount:
            ref = struct.unpack_from('<i', data, p)[0]; p += 4
        else:
            ref = None
        if shown < n_show:
            ref_str = f"  refcnt={ref}" if ref is not None else ""
            print(f"       String[{i}] = {repr(s[:80])}{ref_str}")
            shown += 1

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

    # --- UE Version ---
    version = data[16]
    version_names = {0: 'Legacy', 1: 'Compact', 2: 'Optimized_CRC32', 3: 'Optimized_CityHash64'}
    is_v3 = version >= 3
    print(f"[0x10] UE version : {version} ({version_names.get(version, 'Unknown')})")

    # ----------------------------------------------------------------
    # Determine layout: Standard vs NTE
    # Standard Compact v1:   magic(16)+ue_ver(1)+offset(8)              = 25 bytes header
    # NTE v1 unencrypted:    magic(16)+ue_ver(1)+nte_ver(4)+offset(8)   = 29 bytes header
    # NTE v3 encrypted:      magic(16)+ue_ver(1)+nte_ver(4)+enc(4)+offset(8) = 33 bytes header
    # ----------------------------------------------------------------

    # Peek at pos 17 as int32 to detect NTE version field
    peek_int32 = struct.unpack_from('<i', data, 17)[0] if size >= 21 else 0

    # NTE versions are 1 or 10000+; standard offset would be a large number (>= 25)
    is_nte = (peek_int32 == 1) or (peek_int32 >= 10000)

    # ----------------------------------------------------------------
    # STANDARD COMPACT interpretation
    # ----------------------------------------------------------------
    print(f"\n{'─'*60}")
    print("INTERPRETATION A – Standard Compact")
    print(f"{'─'*60}")
    std_offset = struct.unpack_from('<q', data, 17)[0] if size >= 25 else -1
    print(f"[0x11] String table offset (int64) = {std_offset:,}  (0x{std_offset:X})")
    if 0 <= std_offset < size:
        str_count_std = struct.unpack_from('<I', data, std_offset)[0]
        print(f"       String count at offset   = {str_count_std:,}")
        print_string_table(data, std_offset, min(5, str_count_std), has_refcount=is_v3, n_show=5)
    else:
        print(f"       Offset {std_offset:,} is OUT OF FILE (file is {size:,} bytes)  ← WRONG OFFSET")

    # ----------------------------------------------------------------
    # NTE COMPACT interpretation
    # ----------------------------------------------------------------
    print(f"\n{'─'*60}")
    print("INTERPRETATION B – NTE Compact (game's custom reader)")
    print(f"{'─'*60}")
    nte_version = struct.unpack_from('<i', data, 17)[0] if size >= 21 else 0
    print(f"[0x11] NTE version (int32) @ 17 = {nte_version}")

    if nte_version >= 10100:
        # UE4 serializes bool as int32 (4 bytes)
        is_encrypted = bool(struct.unpack_from('<i', data, 21)[0])
        nte_offset_pos = 25
        key_section_start = 33
        print(f"[0x15] isEncrypted (int32) @ 21 = {is_encrypted}")
    elif nte_version >= 10000:
        is_encrypted = True
        nte_offset_pos = 21
        key_section_start = 29
        print(f"       isEncrypted               = True (implicit, version 10000-10099)")
    else:
        is_encrypted = False
        nte_offset_pos = 21
        key_section_start = 29
        print(f"       isEncrypted               = False (version < 10000, unencrypted)")

    if nte_offset_pos + 8 <= size:
        nte_offset = struct.unpack_from('<q', data, nte_offset_pos)[0]
        print(f"[0x{nte_offset_pos:02X}] String table offset (int64) = {nte_offset:,}  (0x{nte_offset:X})")
        print(f"       Key section starts @ pos {key_section_start}")

        if 0 <= nte_offset < size:
            str_count_nte = struct.unpack_from('<I', data, nte_offset)[0]
            print(f"       String count at NTE offset = {str_count_nte:,}")
            print_string_table(data, nte_offset, min(10, str_count_nte),
                               has_refcount=is_v3, n_show=10)
        else:
            print(f"       NTE offset {nte_offset:,} is OUT OF FILE  ← WRONG OFFSET")
    else:
        print("       Not enough bytes to read NTE offset")
        nte_offset = -1

    # ----------------------------------------------------------------
    # RAW BYTES at key positions
    # ----------------------------------------------------------------
    print(f"\n{'─'*60}")
    print("RAW BYTES at critical positions")
    print(f"{'─'*60}")
    for label, pos, n in [
        ("Pos  17-32 (NTE header area)",     17, 16),
        ("Pos  33-48 (key section start)",   33, 16),
        ("Pos  49-64",                       49, 16),
    ]:
        if pos < size:
            print(f"  {label}")
            print(f"    HEX: {hex_bytes(data, pos, min(n, size-pos))}")

    # ----------------------------------------------------------------
    # KEY SECTION WALK
    # Walk using the correct layout based on detected format.
    # ----------------------------------------------------------------
    if is_nte and nte_offset > 0:
        if is_v3:
            walk_key_section_v3(data, key_section_start, nte_offset, "NTE encrypted v3")
        else:
            walk_key_section_v1(data, key_section_start, nte_offset, "NTE v1 unencrypted")
    else:
        # Standard: key section at pos 25
        if is_v3:
            walk_key_section_v3(data, 25, std_offset, "Standard v3")
        else:
            walk_key_section_v1(data, 25, std_offset, "Standard v1")

    print()

if __name__ == '__main__':
    main()
