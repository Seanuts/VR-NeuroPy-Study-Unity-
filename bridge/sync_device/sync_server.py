# TODO: make this more robust
# run the server in a thread and make it only allow 1 connection at a time
# allow server to be stopped in a seperate thread (one with console)
# heartbeats? might be overkill since its localhost and you can easily
# diagnose connection issues

import socket
import serial
import time

BAUD_RATE = 115200
SER_PORT = "COM4"
SER_TIMEOUT = 1

PACKET_SIZE = 4
HOST = "127.0.0.1"
PORT = 12345
TIMEOUT = 1

class SyncServer:
    def __init__(self):
        # setup server
        self.server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.server.bind((HOST, PORT))
        self.server.settimeout(TIMEOUT)
        # TODO: add timeout so server can check for shutdown
        # setup serial
        self.serial = serial.Serial()
        self.serial.baudrate = BAUD_RATE
        self.serial.port = SER_PORT
        self.serial.timeout = SER_TIMEOUT
        self.serial.open()
        time.sleep(2) # give arduino device time to reset
        print("Ready to receive incoming connections!")

    def cleanup(self):
        print("Shutting down server + serial!")
        self.server.close()
        self.serial.close()

    def start(self):
        self.server.listen()
        # TODO: add a cleanup
        # accepts only one connection at a time
        while True:
            try:
                print("Awaiting new connection!")
                conn, addr = self.server.accept()
                print(f"New connection: {addr}!")
                self.handle_connection(conn)
            except socket.timeout:
                continue
            except KeyboardInterrupt:
                print("Keyboard interrupt!")
                self.cleanup()
            except Exception as e:
                print(f"Server Error: {e}")
                self.cleanup()

    def handle_connection(self, conn: socket.socket):
        # continuously read packets
        try:
            while True:
                data = conn.recv(PACKET_SIZE)
                # if client disconnected
                if not data:
                    break
                # process sent data
                cmd = data.decode()
                if cmd == "TRIG":
                    self.send_trigger()
        # cleanup
        except Exception as e:
            print(f"Connection Error: {e}")
        finally:
            print("Client disconnected!")
            conn.close()

    def send_trigger(self):
        self.serial.write(b'TRIG\n')
        self.serial.flush()


if __name__ == "__main__":
    server = SyncServer()
    server.start()
    pass
