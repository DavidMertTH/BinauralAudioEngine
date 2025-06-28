#include <iostream>


extern "C" __declspec(dllexport)
int main() {
    std::cout << "Hello, World!" << std::endl;
    return 0;
}