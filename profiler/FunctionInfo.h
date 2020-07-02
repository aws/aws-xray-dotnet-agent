#pragma once
#include "corprof.h"
#include "corhdr.h"
#include "cor.h"

class FunctionInfo
{
public:
    FunctionInfo(FunctionID functionId, ClassID classId, ModuleID moduleId, mdToken token, LPWSTR functionName, LPWSTR className, LPWSTR assemblyName);
    ~FunctionInfo(void);

    FunctionID GetFunctionID();
    ClassID GetClassID();
    ModuleID GetModuleID();
    mdToken GetToken();
    LPWSTR GetClassName();
    LPWSTR GetFunctionName();
    LPWSTR GetAssemblyName();

private:
    FunctionID functionId;
    ClassID classId;
    ModuleID moduleId;
    mdToken token;
    LPWSTR className;
    LPWSTR functionName;
    LPWSTR assemblyName;
};