#include <winsock2.h>
#include <windows.h>
#include <iostream>
#include <cstdint>

#pragma comment(lib, "Ws2_32.lib")
#pragma comment(lib, "User32.lib")

#pragma pack(push, 1)
struct ClickCommand {
    int32_t x;
    int32_t y;
};
#pragma pack(pop)

void ExecuteClick(int32_t x, int32_t y) {
    SetCursorPos(x, y);

    INPUT inputs[2] = {0};
    inputs[0].type = INPUT_MOUSE;
    inputs[0].mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
    inputs[1].type = INPUT_MOUSE;
    inputs[1].mi.dwFlags = MOUSEEVENTF_LEFTUP;

    SendInput(2, inputs, sizeof(INPUT));
}

int main() {
    WSADATA wsaData;
    WSAStartup(MAKEWORD(2, 2), &wsaData);

    SOCKET listenSocket = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
    sockaddr_in serverAddr{};
    serverAddr.sin_family = AF_INET;
    serverAddr.sin_port = htons(9000);
    serverAddr.sin_addr.s_addr = INADDR_ANY; // Listens on Ethernet adapter

    bind(listenSocket, (sockaddr*)&serverAddr, sizeof(serverAddr));
    std::cout << "Clicker Service Running on Port 9000...\n";

    char buffer[sizeof(ClickCommand)];
    sockaddr_in clientAddr{};
    int clientLen = sizeof(clientAddr);

    while (true) {
        int bytesReceived = recvfrom(listenSocket, buffer, sizeof(buffer), 0, 
                                     (sockaddr*)&clientAddr, &clientLen);

        if (bytesReceived == sizeof(ClickCommand)) {
            ClickCommand* cmd = reinterpret_cast<ClickCommand*>(buffer);
            ExecuteClick(cmd->x, cmd->y);
            
            // NEW: Send Acknowledgment back to Python
            const char* ackMsg = "ACK";
            sendto(listenSocket, ackMsg, strlen(ackMsg), 0, (sockaddr*)&clientAddr, clientLen);
        }
    }
    return 0;
}

//compile with mingw32_m\bin\g++.exe clicker.cpp -o clicker_nopy.exe -luser32 -lws2_32 -static -static-libgcc -static-libstdc++