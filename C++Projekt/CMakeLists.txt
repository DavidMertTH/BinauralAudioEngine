cmake_minimum_required(VERSION 3.15)
project(MyLibrary)

set(CMAKE_CXX_STANDARD 20)

# Erstelle eine DLL
add_library(MyLibrary SHARED
        src/mylib.cpp
        src/mylib.h
)

# Optional: Header sichtbar machen
target_include_directories(MyLibrary PUBLIC ${CMAKE_CURRENT_SOURCE_DIR})

