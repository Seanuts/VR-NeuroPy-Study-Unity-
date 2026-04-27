import socket
import struct

class LatencyBridge:
    def __init__(self, cpp_ip="192.168.1.2", cpp_port=9000, unity_port=8000):
        self.cpp_ip = cpp_ip
        self.cpp_port = cpp_port
        
        # Socket to talk to C++
        self.c_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        # Lowered to 0.5s so the test doesn't freeze Unity for long when C++ is offline
        self.c_sock.settimeout(0.5) 
        
        # Socket to listen to Unity (Localhost)
        self.u_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.u_sock.bind(("127.0.0.1", unity_port))

    def listen_and_bridge(self):
        print("Python Bridge Active. Waiting for Unity Ping...")
        while True:
            # 1. Wait for Unity
            data, unity_addr = self.u_sock.recvfrom(1024)
            
            if data == b"PING":
                # 2. Command C++ (using arbitrary safe coordinates for the test)
                payload = struct.pack('2i', 10, 10) 
                self.c_sock.sendto(payload, (self.cpp_ip, self.cpp_port))
                
                try:
                    # 3. Wait for C++ ACK
                    ack_data, _ = self.c_sock.recvfrom(1024)
                    
                    if ack_data == b"ACK":
                        # 4. Report Full Success back to Unity
                        self.u_sock.sendto(b"PONG", unity_addr)
                        print("Full Pipeline Success: Unity -> Py -> C++ -> Py -> Unity")
                        
                except socket.timeout:
                    # C++ is offline. Tell Unity we skipped it.
                    print("Timeout: C++ Clicker not found. Skipping and replying to Unity directly.")
                    self.u_sock.sendto(b"PONG_NO_CPP", unity_addr)

if __name__ == "__main__":
    # Ensure cpp_ip matches the Windows 7 machine's Ethernet IP for later
    bridge = LatencyBridge(cpp_ip="192.168.1.2") 
    bridge.listen_and_bridge()