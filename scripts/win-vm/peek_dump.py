import struct, sys

path = sys.argv[1]
data = open(path, 'rb').read()
if data[:4] != b'MDMP':
    print('not a minidump'); sys.exit(1)

number_of_streams, stream_dir_rva = struct.unpack_from('<II', data, 8)

streams = {}
for i in range(number_of_streams):
    off = stream_dir_rva + i * 12
    stype, size, rva = struct.unpack_from('<III', data, off)
    streams.setdefault(stype, (size, rva))

print('streams:', sorted(streams))

# ModuleListStream = 4
mods = []
if 4 in streams:
    size, rva = streams[4]
    n = struct.unpack_from('<I', data, rva)[0]
    base = rva + 4
    for i in range(n):
        m = base + i * 108
        base_of_image, size_of_image = struct.unpack_from('<QI', data, m)
        name_rva = struct.unpack_from('<I', data, m + 20)[0]
        if name_rva:
            ln = struct.unpack_from('<I', data, name_rva)[0]
            name = data[name_rva+4:name_rva+4+ln].decode('utf-16-le', 'ignore')
        else:
            name = '?'
        mods.append((base_of_image, size_of_image, name))

def find_mod(addr):
    for base, size, name in mods:
        if base <= addr < base + size:
            return name, addr - base
    return None, None

# ExceptionStream = 6
if 6 in streams:
    size, rva = streams[6]
    tid = struct.unpack_from('<I', data, rva)[0]
    er = rva + 8
    code, flags, record, addr, nparams = struct.unpack_from('<IIQQI', data, er)
    params = struct.unpack_from('<15Q', data, er + 32)
    print(f'exception thread=0x{tid:x} code=0x{code:08X} addr=0x{addr:X} nparams={nparams}')
    print('params:', [hex(p) for p in params[:nparams]])
    name, off = find_mod(addr)
    print('faulting module:', name, hex(off) if off is not None else '')
    # scan the exception thread stack for return addresses in our dll / msctf
    print('stack hits:')
    # find thread context to get rsp
    ctx_size, ctx_rva = struct.unpack_from('<II', data, er + 32 + 15*8)
    # CONTEXT for x64: Rsp at offset 0x98, Rip 0xF8
    rsp = struct.unpack_from('<Q', data, ctx_rva + 0x98)[0]
    rip = struct.unpack_from('<Q', data, ctx_rva + 0xF8)[0]
    print(f'rip=0x{rip:X} rsp=0x{rsp:X}')
    nm, of = find_mod(rip)
    print('rip module:', nm, hex(of) if of is not None else '')
    # read stack memory: prefer Memory64ListStream(9) or MemoryList(5)
    mem = None
    if 9 in streams:
        s9, r9 = streams[9]
        count, base_rva = struct.unpack_from('<QQ', data, r9)
        entries = []
        p = r9 + 16
        for i in range(count):
            start, msize = struct.unpack_from('<QQ', data, p); p += 16
            entries.append((start, msize, base_rva))
            base_rva += msize
        mem = entries
    elif 5 in streams:
        s5, r5 = streams[5]
        count = struct.unpack_from('<I', data, r5)[0]
        entries = []
        p = r5 + 4
        for i in range(count):
            start, msize, mrva = struct.unpack_from('<QII', data, p); p += 16
            entries.append((start, msize, mrva))
        mem = entries
    def read_mem(addr, n):
        for start, msize, mrva in mem or []:
            if start <= addr < start + msize:
                off = mrva + (addr - start)
                return data[off:off+n]
        return None
    raw = read_mem(rsp, 0x400)
    if raw:
        for i in range(0, len(raw) - 7, 8):
            v = struct.unpack_from('<Q', raw, i)[0]
            nm, of = find_mod(v)
            if nm and ('AvlnIme' in nm or 'msctf' in nm.lower() or 'TextInput' in nm):
                print(f'  [rsp+0x{i:X}] 0x{v:X} {nm}+0x{of:X}')
else:
    print('no exception stream')
