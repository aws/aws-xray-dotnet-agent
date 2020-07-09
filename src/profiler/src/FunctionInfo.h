// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma once
#include "corprof.h"
#include "CorHdr.h"
#include "cor.h"

class FunctionInfo
{
public:
    FunctionInfo(FunctionID functionID, ClassID classID, ModuleID moduleID, mdToken token, LPWSTR functionName, LPWSTR className, LPWSTR assemblyName);
    ~FunctionInfo();

    FunctionID GetFunctionID();
    ClassID GetClassID();
    ModuleID GetModuleID();
    mdToken GetToken();
    LPWSTR GetClassName();
    LPWSTR GetFunctionName();
    LPWSTR GetAssemblyName();

private:
    FunctionID functionID;
    ClassID classID;
    ModuleID moduleID;
    mdToken token;
    LPWSTR className;
    LPWSTR functionName;
    LPWSTR assemblyName;
};