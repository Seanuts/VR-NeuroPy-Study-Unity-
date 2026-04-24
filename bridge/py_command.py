import socket
import struct

class FastClicker:
    def __init__(self, ip="127.0.0.1", port=9000):
        self.ip = ip
        self.port = port
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        
    def click_at(self, x: int, y: int):
        """
        Packs X and Y into a C-compatible binary struct and sends it.
        '2i' means two 32-bit signed integers.
        """
        payload = struct.pack('2i', x, y)
        self.sock.sendto(payload, (self.ip, self.port))

if __name__ == "__main__":
    clicker = FastClicker()
    clicker.click_at(600, 1100) #(x,y) ---> right, v down