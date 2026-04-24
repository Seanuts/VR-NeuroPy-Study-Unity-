/**
 * High-Speed UDP Mouse Clicker
 * OS: Windows 7+ Compatible
 * To run:
 * >> cl clicker.cpp
 * >> .\clicker.exe
 */
#include <winsock2.h>
#include <windows.h>
#include <ws2tcpip.h>
#include <iostream>
#include <cstdint>

#pragma comment(lib, "Ws2_32.lib")
#pragma comment(lib, "User32.lib")
// Define the exact 8-byte binary payload expected from Python
#pragma pack(push, 1)
struct ClickCommand {
    int32_t x;
    int32_t y;
};
#pragma pack(pop)
/**
 * Moves the cursor and batches the mouse down/up events into a single 
 * OS-level call to minimize execution latency.
 */
void ExecuteClick(int32_t x, int32_t y) {
    // Move to exact pixel coordinates
    SetCursorPos(x, y);

    // Batch the Down and Up events into a single SendInput call
    INPUT inputs[2] = {0};

    inputs[0].type = INPUT_MOUSE;
    inputs[0].mi.dwFlags = MOUSEEVENTF_LEFTDOWN;

    inputs[1].type = INPUT_MOUSE;
    inputs[1].mi.dwFlags = MOUSEEVENTF_LEFTUP;

    SendInput(2, inputs, sizeof(INPUT));
}

int main() {
    // 1. Initialize Winsock
    WSADATA wsaData;
    if (WSAStartup(MAKEWORD(2, 2), &wsaData) != 0) {
        std::cerr << "WSAStartup failed.\n";
        return 1;
    }

    // 2. Setup UDP Socket
    SOCKET listenSocket = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
    if (listenSocket == INVALID_SOCKET) {
        std::cerr << "Socket creation failed.\n";
        WSACleanup();
        return 1;
    }

    sockaddr_in serverAddr{};
    serverAddr.sin_family = AF_INET;
    serverAddr.sin_port = htons(9000); // Port to listen on
    serverAddr.sin_addr.s_addr = inet_addr("127.0.0.1");

    if (bind(listenSocket, (sockaddr*)&serverAddr, sizeof(serverAddr)) == SOCKET_ERROR) {
        std::cerr << "Bind failed. Port 9000 might be in use.\n";
        closesocket(listenSocket);
        WSACleanup();
        return 1;
    }

    std::cout << "Clicker Service Running. Listening on 127.0.0.1:9000...\n";

    // 3. High-Speed Listening Loop
    char buffer[sizeof(ClickCommand)];
    sockaddr_in clientAddr{};
    int clientLen = sizeof(clientAddr);

    while (true) {
        // Block until a command is received
        int bytesReceived = recvfrom(listenSocket, buffer, sizeof(buffer), 0, 
                                     (sockaddr*)&clientAddr, &clientLen);

        if (bytesReceived == sizeof(ClickCommand)) {
            // Instantly cast the raw bytes to our struct and execute
            ClickCommand* cmd = reinterpret_cast<ClickCommand*>(buffer);
            ExecuteClick(cmd->x, cmd->y);
            
            // Optional: Print for debugging (remove in production for max speed)
            std::cout << "Clicked at X:" << cmd->x << " Y:" << cmd->y << "\n";
        }
    }

    // Cleanup (Unreachable in this infinite loop, but good practice)
    closesocket(listenSocket);
    WSACleanup();
    return 0;
}
