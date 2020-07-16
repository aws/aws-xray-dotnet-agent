// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include "stdafx.h"
#include "ILWriter.h"
#include <corhlpr.cpp>

ILWriter::ILWriter(ICorProfilerInfo* profilerInfo, FunctionInfo* functionInfo)
{
    ModuleID moduleID = functionInfo->GetModuleID();
    mdToken mdtoken = functionInfo->GetToken();
    LPCBYTE methodHeader;
    ULONG methodSize;

    HRESULT hr = profilerInfo->GetILFunctionBody(moduleID, mdtoken, &methodHeader, &methodSize);

    if (FAILED(hr))
    {
        return;
    }

    if (!((COR_ILMETHOD_TINY*)methodHeader)->IsTiny())
    {
        this->identifier = FatMethod;
    }
    else
    {
        if (methodSize <= (MaximumSize - InjectedCodeSize))
        {
            this->identifier = TinyMethod;
        }
        else
        {
            this->identifier = OtherMethod;
        }
    }

    this->profilerInfo = profilerInfo;
    this->functionInfo = functionInfo;
    this->methodHeader = methodHeader;
    this->methodSize = methodSize;
}

ILWriter::~ILWriter()
{
}

BOOL ILWriter::Write()
{
    ModuleID moduleID = functionInfo->GetModuleID();
    mdToken functionToken = functionInfo->GetToken();
    LPCBYTE newILHeader = (LPCBYTE)GetNewILHeader();

    HRESULT hr = profilerInfo->SetILFunctionBody(moduleID, functionToken, newILHeader);

    if (FAILED(hr))
    {
        return FALSE;
    }

    return TRUE;
}

ULONG ILWriter::GetNewMethodTotalSize()
{
    if (identifier == FatMethod)
    {
        return methodSize + InjectedCodeSize + GetOffset();
    }
    else
    {
        return methodSize + InjectedCodeSize;
    }
}

ULONG ILWriter::GetOffset()
{
    if (!(((COR_ILMETHOD_FAT*)methodHeader)->GetFlags() & CorILMethod_MoreSects))
    {
        return 0;
    }
    else
    {
        ULONG oldMethodSize = FatMethodHeader + ((COR_ILMETHOD_FAT*)methodHeader)->GetCodeSize();
        ULONG newMethodSize = FatMethodHeader + ((COR_ILMETHOD_FAT*)methodHeader)->GetCodeSize() + InjectedCodeSize;
        ULONG oldOffset = (int)((BYTE*)((COR_ILMETHOD_FAT*)methodHeader)->GetSect() - (BYTE*)((BYTE*)methodHeader + oldMethodSize));
        ULONG mod = sizeof(DWORD);
        ULONG remainder = newMethodSize % mod;

        ULONG newOffset = 0;
        if (remainder != 0)
        {
            newOffset = mod - remainder;
        }

        return newOffset - oldOffset;
    }
}

void* ILWriter::GetNewILHeader()
{
    ModuleID moduleId = functionInfo->GetModuleID();
    IMethodMalloc* allocator = NULL;
    HRESULT hr = profilerInfo->GetILFunctionBodyAllocator(moduleId, &allocator);
    if (FAILED(hr) || allocator == NULL)
    {
        return NULL;
    }

    IMetaDataEmit* iMetaDataEmit = NULL;
    DWORD OpenFlags = ofRead | ofWrite;
    hr = profilerInfo->GetModuleMetaData(moduleId, OpenFlags, IID_IMetaDataEmit, (IUnknown**)&iMetaDataEmit);
    if (FAILED(hr) || iMetaDataEmit == NULL)
    {
        return NULL;
    }

    ULONG newMethodTotalSize = GetNewMethodTotalSize();
    BYTE* codeBuffer = (BYTE*)allocator->Alloc(newMethodTotalSize);
    if (codeBuffer == NULL)
    {
        return NULL;
    }

    allocator->Release();

    IMetaDataAssemblyEmit* iMetaDataAssemblyEmit = NULL;
    hr = iMetaDataEmit->QueryInterface(IID_IMetaDataAssemblyEmit, (void**)&iMetaDataAssemblyEmit);
    if (FAILED(hr) || iMetaDataAssemblyEmit == NULL)
    {
        return NULL;
    }

    const BYTE publicKey[] = { 0xd4, 0x27, 0x00, 0x1f, 0x96, 0xb0, 0xd0, 0xb6 }; // d427001f96b0d0b6
    LPCWSTR autoInstrumentationAssemblyName = L"AWSXRayRecorder.AutoInstrumentation";
    ASSEMBLYMETADATA autoInstrumentationAssemblyMetaData = {0};
    mdModuleRef autoInstrumentationAssemblyToken;
    hr = iMetaDataAssemblyEmit->DefineAssemblyRef(publicKey, sizeof(publicKey), autoInstrumentationAssemblyName, &autoInstrumentationAssemblyMetaData, NULL, 0, 0, &autoInstrumentationAssemblyToken);
    if (FAILED(hr))
    {
        return NULL;
    }
    
    iMetaDataAssemblyEmit->Release();

    LPCWSTR autoInstrumentationClassName = L"Amazon.XRay.Recorder.AutoInstrumentation.Initialize";
    mdTypeRef autoInstrumentationClassToken;
    hr = iMetaDataEmit->DefineTypeRefByName(autoInstrumentationAssemblyToken, autoInstrumentationClassName, &autoInstrumentationClassToken);
    if (FAILED(hr))
    {
        return NULL;
    }

    LPCWSTR autoInstrumentationMethodName = L"AddXRay";
    const BYTE autoInstrumentationMethodSignature[] = { IMAGE_CEE_CS_CALLCONV_DEFAULT, 0, ELEMENT_TYPE_VOID }; //0 arg, void
    mdMemberRef autoInstrumentationMethodToken;
    hr = iMetaDataEmit->DefineMemberRef(autoInstrumentationClassToken, autoInstrumentationMethodName, autoInstrumentationMethodSignature, sizeof(autoInstrumentationMethodSignature), &autoInstrumentationMethodToken);
    if (FAILED(hr))
    {
        return NULL;
    }

    iMetaDataEmit->Release();

    InjectedCode* injectedCode = new InjectedCode();
    injectedCode->nop = 0x00;
    injectedCode->call = 0x28;

    if (identifier == FatMethod)
    {
        memcpy_s(
            codeBuffer,
            newMethodTotalSize,
            methodHeader,
            FatMethodHeader);

        WORD maxStack = (WORD)((COR_ILMETHOD_FAT*)methodHeader)->MaxStack + InjectedCodeSize / 2;
        memcpy_s(
            codeBuffer + sizeof(WORD),
            newMethodTotalSize - sizeof(WORD),
            &maxStack,
            sizeof(WORD));

        DWORD newMethodBodySize = ((COR_ILMETHOD_FAT*)methodHeader)->GetCodeSize() + InjectedCodeSize;
        memcpy_s(
            codeBuffer + sizeof(DWORD),
            newMethodTotalSize - sizeof(DWORD),
            &newMethodBodySize,
            sizeof(DWORD));

        memcpy_s(
            injectedCode->token,
            sizeof(injectedCode->token),
            (void*)&autoInstrumentationMethodToken,
            sizeof(autoInstrumentationMethodToken));

        memcpy_s(
            codeBuffer + FatMethodHeader,
            newMethodTotalSize - FatMethodHeader,
            injectedCode,
            InjectedCodeSize);

        ULONG oldMethodBodySize = ((COR_ILMETHOD_FAT*)methodHeader)->GetCodeSize();

        memcpy_s(
            codeBuffer + FatMethodHeader + InjectedCodeSize,
            newMethodTotalSize - FatMethodHeader - InjectedCodeSize,
            (BYTE*)methodHeader + FatMethodHeader,
            oldMethodBodySize);

        ULONG oldMethodSize = FatMethodHeader + ((COR_ILMETHOD_FAT*)methodHeader)->GetCodeSize();
        ULONG extraSectionSize = methodSize - oldMethodSize;
        memcpy_s(
            codeBuffer + FatMethodHeader + InjectedCodeSize + oldMethodBodySize,
            newMethodTotalSize - FatMethodHeader - InjectedCodeSize - oldMethodBodySize + GetOffset(),
            (BYTE*)methodHeader + (methodSize - extraSectionSize - GetOffset()),
            extraSectionSize);

        if (((COR_ILMETHOD_FAT*)methodHeader)->GetFlags() & CorILMethod_MoreSects)
        {
            FixSEHSections(codeBuffer, InjectedCodeSize);
        }
    }
    else if (identifier == TinyMethod)
    {
        ULONG newMethodBodySize = ((COR_ILMETHOD_TINY*)methodHeader)->GetCodeSize() + InjectedCodeSize;

        BYTE newBodySize = (BYTE)(CorILMethod_TinyFormat | (newMethodBodySize << 2));
        memcpy_s(
            codeBuffer,
            newMethodTotalSize,
            &newBodySize,
            TinyMethodHeader);
        memcpy_s(
            injectedCode->token,
            sizeof(injectedCode->token),
            (void*)&autoInstrumentationMethodToken,
            sizeof(autoInstrumentationMethodToken));
        memcpy_s(
            codeBuffer + TinyMethodHeader,
            newMethodTotalSize - TinyMethodHeader,
            injectedCode,
            InjectedCodeSize);

        ULONG oldMethodBodySize = ((COR_ILMETHOD_TINY*)methodHeader)->GetCodeSize();

        memcpy_s(
            codeBuffer + TinyMethodHeader + InjectedCodeSize,
            newMethodTotalSize - TinyMethodHeader - InjectedCodeSize,
            (BYTE*)methodHeader + TinyMethodHeader,
            oldMethodBodySize);

    }
    else if (identifier == OtherMethod)
    {
        BYTE flags[] = { 0x03, 0x30, };
        memcpy_s(
            codeBuffer,
            newMethodTotalSize,
            &flags,
            sizeof(WORD));

        WORD maxStack = 1 + (InjectedCodeSize / 2);
        memcpy_s(
            codeBuffer + sizeof(WORD),
            newMethodTotalSize - sizeof(WORD),
            &maxStack,
            sizeof(WORD));

        DWORD newMethodBodySize = ((COR_ILMETHOD_TINY*)methodHeader)->GetCodeSize() + InjectedCodeSize;
        memcpy_s(
            codeBuffer + sizeof(DWORD),
            newMethodTotalSize - sizeof(DWORD),
            &newMethodBodySize,
            sizeof(DWORD));
        memcpy_s(
            injectedCode->token,
            sizeof(injectedCode->token),
            (void*)&autoInstrumentationMethodToken,
            sizeof(autoInstrumentationMethodToken));
        memcpy_s(
            codeBuffer + FatMethodHeader,
            newMethodTotalSize - FatMethodHeader,
            injectedCode,
            InjectedCodeSize);

        ULONG oldMethodBodySize = ((COR_ILMETHOD_TINY*)methodHeader)->GetCodeSize();

        memcpy_s(
            codeBuffer + FatMethodHeader + InjectedCodeSize,
            newMethodTotalSize - FatMethodHeader - InjectedCodeSize,
            (BYTE*)methodHeader + TinyMethodHeader,
            oldMethodBodySize);
    }

    return codeBuffer;
}

void ILWriter::FixSEHSections(BYTE* codeBuffer, ULONG offset)
{
    COR_ILMETHOD_FAT* fatMethod = (COR_ILMETHOD_FAT*)codeBuffer;

    const COR_ILMETHOD_SECT* sections = fatMethod->GetSect();

    while (sections)
    {
        if (sections->Kind() == CorILMethod_Sect_EHTable)
        {
            COR_ILMETHOD_SECT_EH* eh = (COR_ILMETHOD_SECT_EH*)sections;

            if (eh->IsFat())
            {
                COR_ILMETHOD_SECT_EH_FAT* EHFat = (COR_ILMETHOD_SECT_EH_FAT*)eh;

                for (UINT i = 0; i < eh->EHCount(); i++)
                {
                    IMAGE_COR_ILMETHOD_SECT_EH_CLAUSE_FAT* clause = &EHFat->Clauses[i];

                    if (clause->Flags & COR_ILEXCEPTION_CLAUSE_FILTER)
                    {
                        clause->FilterOffset += offset;
                    }

                    clause->TryOffset += offset;
                    clause->HandlerOffset += offset;
                }
            }
            else
            {
                COR_ILMETHOD_SECT_EH_SMALL* EHSmall = (COR_ILMETHOD_SECT_EH_SMALL*)eh;

                for (UINT i = 0; i < eh->EHCount(); i++)
                {
                    IMAGE_COR_ILMETHOD_SECT_EH_CLAUSE_SMALL* clause = &EHSmall->Clauses[i];

                    if (clause->Flags & COR_ILEXCEPTION_CLAUSE_FILTER)
                    {
                        clause->FilterOffset += offset;
                    }

                    clause->TryOffset += offset;
                    clause->HandlerOffset += offset;
                }
            }
        }

        sections = sections->Next();
    }
}
