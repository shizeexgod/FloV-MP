"""Run from this directory with: python3 test_udp_protocol.py"""

import unittest

from udp_protocol import check_cookie, make_cookie, pack_packet, unpack_packet


class UdpProtocolTests(unittest.TestCase):
    def test_packet_vector(self):
        packet = pack_packet(7, 0x0102030405060708, 0x11223344, 0x55667788,
                             0x99aabbcc, b"\x01\x02\x03")
        self.assertEqual(packet.hex(),
                         "464c5633030700000102030405060708112233445566778899aabbcc00030000010203")
        self.assertEqual(unpack_packet(packet),
                         (7, 0x0102030405060708, 0x11223344, 0x55667788,
                          0x99aabbcc, b"\x01\x02\x03"))
        with self.assertRaises(ValueError):
            unpack_packet(packet[:-1])

    def test_cookie_vector_and_binding(self):
        secret, nonce = bytes(range(32)), bytes(range(16))
        cookie = make_cookie(secret, "192.0.2.7", 7798, nonce, 1700000000)
        self.assertEqual(cookie.hex(), "000000000a21fe80bbbb190d0c33cb6c518f314d1879226c")
        self.assertTrue(check_cookie(secret, "::ffff:192.0.2.7", 7798, nonce,
                                     cookie, 1700000010))
        self.assertFalse(check_cookie(secret, "192.0.2.8", 7798, nonce,
                                      cookie, 1700000000))
        self.assertFalse(check_cookie(secret, "192.0.2.7", 7798, nonce,
                                      cookie, 1700000020))


if __name__ == "__main__":
    unittest.main()
