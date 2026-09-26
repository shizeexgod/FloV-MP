"""FLOV/3 wire codec for the Python test bot. No UDP game transport yet."""

import hashlib
import hmac
import ipaddress
import struct

MAGIC = b"FLV3"
VERSION = 3
HEADER = struct.Struct(">4sBBBBQIIIHH")
HEADER_SIZE = HEADER.size
MAX_DATAGRAM = 1200
MAX_PAYLOAD = MAX_DATAGRAM - HEADER_SIZE
COOKIE_SIZE = 24
NONCE_SIZE = 16
COOKIE_DOMAIN = b"FLOVMP-UDP-COOKIE-v3"
PACKET_TYPES = set(range(1, 10))


def pack_packet(kind, connection_id=0, sequence=0, ack=0, ack_bits=0, payload=b""):
    if kind not in PACKET_TYPES or len(payload) > MAX_PAYLOAD:
        raise ValueError("invalid type or payload length")
    return HEADER.pack(MAGIC, VERSION, kind, 0, 0, connection_id,
                       sequence, ack, ack_bits, len(payload), 0) + payload


def unpack_packet(data):
    if not HEADER_SIZE <= len(data) <= MAX_DATAGRAM:
        raise ValueError("invalid datagram length")
    magic, version, kind, flags, reserved, cid, seq, ack, ack_bits, size, tail = HEADER.unpack_from(data)
    if (magic != MAGIC or version != VERSION or kind not in PACKET_TYPES or
            flags or reserved or tail or size > MAX_PAYLOAD or len(data) != HEADER_SIZE + size):
        raise ValueError("invalid header")
    return (kind, cid, seq, ack, ack_bits, data[HEADER_SIZE:])


def _mapped_ip(address):
    ip = ipaddress.ip_address(address)
    if isinstance(ip, ipaddress.IPv4Address):
        ip = ipaddress.IPv6Address("::ffff:" + str(ip))
    return ip.packed


def make_cookie(secret, address, port, nonce, unix_seconds):
    if len(secret) != 32 or len(nonce) != NONCE_SIZE or not 0 <= port <= 65535:
        raise ValueError("invalid cookie input")
    bucket = unix_seconds // 10
    msg = (COOKIE_DOMAIN + bytes([VERSION]) + _mapped_ip(address) +
           struct.pack(">H", port) + nonce + struct.pack(">q", bucket))
    return struct.pack(">q", bucket) + hmac.new(secret, msg, hashlib.sha256).digest()[:16]


def check_cookie(secret, address, port, nonce, cookie, unix_seconds):
    if len(cookie) != COOKIE_SIZE:
        return False
    bucket = struct.unpack_from(">q", cookie)[0]
    if bucket not in (unix_seconds // 10, unix_seconds // 10 - 1):
        return False
    return hmac.compare_digest(make_cookie(secret, address, port, nonce, bucket * 10), cookie)
