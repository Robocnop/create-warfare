"""Write servers.dat (multiplayer server list) at the pack root.

packwiz exports it in the CurseForge zip overrides, so a fresh import already lists the server.
Players who already have a list get the server added by the launcher (servers in version.json),
without losing theirs. SERVERS is the single source for both.

Usage: python tools/gen_servers_dat.py
"""
import os, struct

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

SERVERS = [
    {"name": "Create Warfare", "ip": "jeu.robocnop.be:27018"},
]

TAG_END, TAG_STRING, TAG_LIST, TAG_COMPOUND = 0, 8, 9, 10


def nbt_string(s):
    # Java modified UTF-8 equals UTF-8 for these ASCII values; refuse anything else rather than guess.
    data = s.encode("ascii")
    return struct.pack(">H", len(data)) + data


def servers_dat(servers):
    out = bytes([TAG_COMPOUND]) + nbt_string("")
    out += bytes([TAG_LIST]) + nbt_string("servers") + bytes([TAG_COMPOUND]) + struct.pack(">i", len(servers))
    for s in servers:
        for key in ("name", "ip"):
            out += bytes([TAG_STRING]) + nbt_string(key) + nbt_string(s[key])
        out += bytes([TAG_END])
    return out + bytes([TAG_END])


if __name__ == "__main__":
    path = os.path.join(ROOT, "servers.dat")
    with open(path, "wb") as f:
        f.write(servers_dat(SERVERS))
    print("written", path, [s["ip"] for s in SERVERS])
