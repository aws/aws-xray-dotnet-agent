#pragma once
#include "FunctionInfo.h"

#define FatMethod 1
#define TinyMethod 2
#define OtherMethod 3
#define MaximumSize 64

typedef struct
{
    BYTE nop;
    BYTE call;
    BYTE callToken[4];
} InjectionCode;


class ILWriter
{
public:    
    ILWriter(ICorProfilerInfo* profilerInfo, FunctionInfo* functionInfo);
    
    ~ILWriter(void);

    BOOL Write();
    BOOL IsWritable();
    void* GetNewILMethodHeader();

protected:
    ULONG GetOffSet();
    void FixSEHSections(LPCBYTE methodBytes, ULONG newILSize);

private:
    ULONG identifier = 0;
    ICorProfilerInfo* profilerInfo = NULL;
    FunctionInfo* functionInfo = NULL;
    LPCBYTE methodHeader = NULL;
    ULONG methodSize = 0;
};