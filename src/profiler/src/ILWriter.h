// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma once
#include "FunctionInfo.h"

#define FatMethod 1
#define TinyMethod 2
#define OtherMethod 3
#define MaximumSize 64

#define FatMethodHeader sizeof(WORD) + sizeof(WORD) + sizeof(DWORD) + sizeof(DWORD) // 12 bytes 
#define TinyMethodHeader sizeof(BYTE) 
#define InjectedCodeSize sizeof(InjectedCode)

typedef struct
{
    BYTE nop;
    BYTE call;
    BYTE token[4];
} InjectedCode;


class ILWriter
{
public:    
    ILWriter(ICorProfilerInfo* profilerInfo, FunctionInfo* functionInfo);
    
    ~ILWriter();

    ULONG GetNewMethodTotalSize();

    BOOL Write();
    void* GetNewILHeader();

    ULONG GetOffset();
    void FixSEHSections(BYTE* methodBytes, ULONG newILSize);

private:

    ULONG identifier = 0;
    ICorProfilerInfo* profilerInfo = NULL;
    FunctionInfo* functionInfo = NULL;
    LPCBYTE methodHeader = NULL;
    ULONG methodSize = 0;
};
