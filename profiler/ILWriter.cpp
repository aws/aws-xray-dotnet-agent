#include "helper.h"
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
        if (methodSize <= (MaximumSize - sizeof(InjectionCode)))
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

ILWriter::~ILWriter(void)
{
}

BOOL ILWriter::Write()
{
    if (IsWritable())
    {
        ModuleID moduleID = functionInfo->GetModuleID();
        mdToken functionToken = functionInfo->GetToken();
        LPCBYTE newILMethodHeader = (LPCBYTE)GetNewILMethodHeader();

        HRESULT hr = profilerInfo->SetILFunctionBody(moduleID, functionToken, newILMethodHeader);

        if (FAILED(hr))
        {
            return FALSE;
        }

        return TRUE;
    }

    return FALSE;
}

ULONG ILWriter::GetOffSet()
{
    BOOL hasExtraSection = (((COR_ILMETHOD_FAT*)methodHeader)->GetFlags() & CorILMethod_MoreSects);
    if (!hasExtraSection)
    {
        return 0;
    }
    else
    {
        ULONG oldMethod = (int)((BYTE*)((COR_ILMETHOD_FAT*)methodHeader)->GetSect() - (BYTE*)((BYTE*)methodHeader + 12 + ((COR_ILMETHOD_FAT*)methodHeader)->GetCodeSize()));
        ULONG total = ((sizeof(InjectionCode) + 12 + ((COR_ILMETHOD_FAT*)methodHeader)->GetCodeSize()) % sizeof(DWORD));

        ULONG newMethod = 0;
        if (total != 0)
        {
            newMethod = sizeof(DWORD) - total;
        }

        return newMethod - oldMethod;
    }
}

BOOL ILWriter::IsWritable()
{
    if (identifier == FatMethod)
    {
        BOOL hasExtraSection = (((COR_ILMETHOD_FAT*)methodHeader)->GetFlags() & CorILMethod_MoreSects);
        if (hasExtraSection)
        {
            COR_ILMETHOD_DECODER decorder((const COR_ILMETHOD*)methodHeader);
            COR_ILMETHOD_SECT_EH* peh = (COR_ILMETHOD_SECT_EH*)decorder.EH;

            do
            {
                for (UINT i = 0; i < peh->EHCount(); ++i)
                {
                    if (!peh->IsFat() && ((peh->Small.Clauses[i].TryOffset + sizeof(InjectionCode)) > 0xFFFF || (peh->Small.Clauses[i].HandlerOffset + sizeof(InjectionCode)) > 0xFFFF))
                    {
                        return FALSE;
                    }
                }

                do
                {
                    peh = (COR_ILMETHOD_SECT_EH*)peh->Next();
                } while (peh && (peh->Kind() & CorILMethod_Sect_KindMask) != CorILMethod_Sect_EHTable);

            } while (peh);
        }

        return TRUE;
    }
    else
    {
        return TRUE;
    }
}

void* ILWriter::GetNewILMethodHeader()
{
    IMethodMalloc* iMethodMalloc = NULL;
    ModuleID moduleId = functionInfo->GetModuleID();

    HRESULT hr = profilerInfo->GetILFunctionBodyAllocator(moduleId, &iMethodMalloc);
    if (FAILED(hr))
    {
        return NULL;
    }

    ULONG newMethodCodeSize;

    if (identifier == FatMethod)
    {
        ULONG offSet = GetOffSet();
        newMethodCodeSize = methodSize + sizeof(InjectionCode) + offSet;
    }
    else
    {
        newMethodCodeSize = methodSize + sizeof(InjectionCode);
    }

    void* newAddress = iMethodMalloc->Alloc(newMethodCodeSize);
    iMethodMalloc->Release();

    if (identifier == FatMethod)
    {
        ULONG fatMethodHeaderSize = sizeof(WORD) + sizeof(WORD) + sizeof(DWORD) + sizeof(DWORD);
        memcpy((BYTE*)newAddress, methodHeader, fatMethodHeaderSize);

        WORD maxStack = (WORD)((COR_ILMETHOD_FAT*)methodHeader)->MaxStack + (sizeof(InjectionCode) / 2);
        memcpy((BYTE*)newAddress + sizeof(WORD), &maxStack, sizeof(WORD));

        DWORD newBodySize = ((COR_ILMETHOD_FAT*)methodHeader)->GetCodeSize() + sizeof(InjectionCode);
        memcpy((BYTE*)newAddress + sizeof(WORD) + sizeof(WORD), &newBodySize, sizeof(DWORD));
    }
    else if (identifier == TinyMethod) 
    {
        ULONG newMethodBodySize = ((COR_ILMETHOD_TINY*)methodHeader)->GetCodeSize() + sizeof(InjectionCode);
        BYTE newBodySize = (BYTE)(CorILMethod_TinyFormat | (newMethodBodySize << 2));
        memcpy((BYTE*)newAddress, &newBodySize, sizeof(BYTE));
    }
    else if (identifier == OtherMethod)
    {
        BYTE flags[] = { 0x03, 0x30, };
        memcpy((BYTE*)newAddress, &flags, sizeof(WORD));

        WORD maxStack = 1 + (sizeof(InjectionCode) / 2);
        memcpy((BYTE*)newAddress + sizeof(WORD), &maxStack, sizeof(WORD));

        DWORD newBodySize = ((COR_ILMETHOD_TINY*)methodHeader)->GetCodeSize() + sizeof(InjectionCode);
        memcpy((BYTE*)newAddress + sizeof(WORD) + sizeof(WORD), &newBodySize, sizeof(DWORD));
    }

    IMetaDataEmit* iMetaDataEmit = NULL;
    hr = profilerInfo->GetModuleMetaData(moduleId, ofRead | ofWrite, IID_IMetaDataEmit, (IUnknown**)&iMetaDataEmit);

    if (FAILED(hr))
    {
        return NULL;
    }
    
    IMetaDataAssemblyEmit* iMetaDataAssemblyEmit = NULL;
    mdModuleRef assemblyToken;
    
    hr = iMetaDataEmit->QueryInterface(IID_IMetaDataAssemblyEmit, (void**)&iMetaDataAssemblyEmit);
    if (FAILED(hr))
    {
        return NULL;
    }

    ASSEMBLYMETADATA assemblyMetaData = { 0 };
    assemblyMetaData.usMajorVersion = 1; // version 1.0.0.0
    // d427001f96b0d0b6
    const BYTE publishKey[] = { 0xd4, 0x27, 0x00, 0x1f, 0x96, 0xb0, 0xd0, 0xb6 };
    LPCWSTR assemblyName = L"AWSXRayRecorder.AutoInstrumentation";
    hr = iMetaDataAssemblyEmit->DefineAssemblyRef(publishKey, sizeof(publishKey), assemblyName, &assemblyMetaData, NULL, 0, 0, &assemblyToken);
    if (FAILED(hr))
    {
        return NULL;
    }

    iMetaDataAssemblyEmit->Release();

    LPCWSTR className = L"Amazon.XRay.Recorder.AutoInstrumentation.Initialize";
    mdTypeRef AWSXRayAutoInstrumentationClassToken;
    hr = iMetaDataEmit->DefineTypeRefByName(assemblyToken, className, &AWSXRayAutoInstrumentationClassToken);
    if (FAILED(hr))
    {
        return NULL;
    }

    mdMemberRef AddXRayMethodToken;
    const BYTE AddXRaySignature[] = { IMAGE_CEE_CS_CALLCONV_DEFAULT, 0, ELEMENT_TYPE_VOID }; //0 arg, void
    LPCWSTR methodName = L"AddXRay";
    hr = iMetaDataEmit->DefineMemberRef(AWSXRayAutoInstrumentationClassToken, methodName, AddXRaySignature, sizeof(AddXRaySignature), &AddXRayMethodToken);
    if (FAILED(hr))
    {
        return NULL;
    }

    iMetaDataEmit->Release();

    InjectionCode* injectionCode = new InjectionCode();
    injectionCode->nop = 0x00;
    injectionCode->call = 0x28;
    memcpy(injectionCode->callToken, (void*)&AddXRayMethodToken, sizeof(AddXRayMethodToken));

    ULONG oldMethodHeaderSize = 0;
    ULONG insertedMethodHeaderSize = 0;
    ULONG oldMethodCodeSize = 0;

    if (identifier == FatMethod)
    {
        oldMethodHeaderSize = sizeof(WORD) + sizeof(WORD) + sizeof(DWORD) + sizeof(DWORD);
        insertedMethodHeaderSize = 12; // Fat header is 12 byte in size
        oldMethodCodeSize = ((COR_ILMETHOD_FAT*)methodHeader)->GetCodeSize();
    }
    else if (identifier == TinyMethod)
    {
        oldMethodHeaderSize = sizeof(BYTE);
        insertedMethodHeaderSize = 1; // Tiny header is 1 byte in size
        oldMethodCodeSize = ((COR_ILMETHOD_TINY*)methodHeader)->GetCodeSize();
    }
    else if (identifier == OtherMethod)
    {
        oldMethodHeaderSize = sizeof(WORD) + sizeof(WORD) + sizeof(DWORD) + sizeof(DWORD);
        insertedMethodHeaderSize = 1;
        oldMethodCodeSize = ((COR_ILMETHOD_TINY*)methodHeader)->GetCodeSize();
    }

    memcpy((BYTE*)newAddress + oldMethodHeaderSize, injectionCode, sizeof(injectionCode));

    memcpy((BYTE*)newAddress + oldMethodHeaderSize + sizeof(injectionCode), (BYTE*)methodHeader + insertedMethodHeaderSize, oldMethodCodeSize);

    if (identifier == FatMethod)
    {
        ULONG offSet = GetOffSet();
        ULONG sectionSize = methodSize - (12 + ((COR_ILMETHOD_FAT*)methodHeader)->GetCodeSize());
        memcpy((BYTE*)newAddress + oldMethodHeaderSize + sizeof(injectionCode) + oldMethodCodeSize, (BYTE*)methodHeader + (methodSize - sectionSize - offSet), sectionSize);

        BOOL hasExtraSection = (((COR_ILMETHOD_FAT*)methodHeader)->GetFlags() & CorILMethod_MoreSects);

        if (hasExtraSection)
        {
            FixSEHSections((LPCBYTE)newAddress, sizeof(injectionCode));
        }
    }

    return newAddress;
}

void ILWriter::FixSEHSections(LPCBYTE size, ULONG offSet)
{
    COR_ILMETHOD_DECODER decorder((const COR_ILMETHOD*)size);
    COR_ILMETHOD_SECT_EH* peh = (COR_ILMETHOD_SECT_EH*)decorder.EH;

    do
    {
        for (UINT i = 0; i < peh->EHCount(); ++i)
        {
            if (peh->IsFat())
            {
                if (peh->Fat.Clauses[i].Flags == COR_ILEXCEPTION_CLAUSE_FILTER)
                {
                    peh->Fat.Clauses[i].FilterOffset += offSet;
                }

                peh->Fat.Clauses[i].TryOffset += offSet;
                peh->Fat.Clauses[i].HandlerOffset += offSet;
            }
            else
            {
                if (peh->Small.Clauses[i].Flags == COR_ILEXCEPTION_CLAUSE_FILTER)
                {
                    peh->Small.Clauses[i].FilterOffset += offSet;
                }

                peh->Small.Clauses[i].TryOffset += offSet;
                peh->Small.Clauses[i].HandlerOffset += offSet;
            }
        }

        do
        {
            peh = (COR_ILMETHOD_SECT_EH*)peh->Next();
        } while (peh && (peh->Kind() & CorILMethod_Sect_KindMask) != CorILMethod_Sect_EHTable);

    } while (peh);
}