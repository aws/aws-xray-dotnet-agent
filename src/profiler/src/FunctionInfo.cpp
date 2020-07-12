// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include "stdafx.h"
#include "FunctionInfo.h"

FunctionInfo::FunctionInfo(FunctionID functionID, ClassID classID, ModuleID moduleID, mdToken token, LPWSTR functionName, LPWSTR className, LPWSTR assemblyName)
{
    this->functionID = functionID;
    this->classID = classID;
    this->moduleID = moduleID;
    this->token = token;
    this->assemblyName = assemblyName;
    this->functionName = functionName;
    this->className = className;
}

FunctionInfo::~FunctionInfo()
{
}

FunctionID FunctionInfo::GetFunctionID()
{
    return functionID;
}

ClassID FunctionInfo::GetClassID()
{
    return classID;
}

ModuleID FunctionInfo::GetModuleID()
{
    return moduleID;
}

mdToken FunctionInfo::GetToken()
{
    return token;
}

LPWSTR FunctionInfo::GetClassName()
{
    return className;
}

LPWSTR FunctionInfo::GetFunctionName()
{
    return functionName;
}

LPWSTR FunctionInfo::GetAssemblyName()
{
    return assemblyName;
}
