#include "helper.h"
#include "FunctionInfo.h"

FunctionInfo::FunctionInfo(FunctionID functionId, ClassID classId, ModuleID moduleId, mdToken token, LPWSTR functionName, LPWSTR className, LPWSTR assemblyName)
{
    this->functionId = functionId;
    this->classId = classId;
    this->moduleId = moduleId;
    this->token = token;
    this->assemblyName = assemblyName;
    this->functionName = functionName;
    this->className = className;
}

FunctionInfo::~FunctionInfo(void)
{
    free(functionName);
    free(className);
    free(assemblyName);
}

FunctionID FunctionInfo::GetFunctionID()
{
    return functionId;
}

ClassID FunctionInfo::GetClassID()
{
    return classId;
}

ModuleID FunctionInfo::GetModuleID()
{
    return moduleId;
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