#pragma once

#ifdef _WIN32
  #ifdef MYLIBRARY_EXPORTS
    #define MY_API __declspec(dllexport)
  #else
    #define MY_API __declspec(dllimport)
  #endif
#else
  #define MY_API
#endif

extern "C" {
    MY_API int add(int a, int b);
}
