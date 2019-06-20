// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once
#include <vector>
#include <string>
#include <sstream>

using namespace std;


struct _Option
{
  virtual const vector<wstring>& Strings() = 0;
  virtual const wstring& Description() = 0;
  virtual bool HasParam() = 0;
  virtual const wstring& Param() = 0;
  virtual void Call(const wstring& arg) = 0;
  virtual void Call() = 0;
};

template <typename Function>
class Option : public _Option
{
  vector<wstring> optStrings;
  const Function& func;
  wstring description;

  Option& operator=(const Option&) = delete;

public:
  Option(vector<wstring> s, const Function& f, const wstring& desc) :optStrings(move(s)), func(f), description(desc)
  {
  }

  virtual const vector<wstring>& Strings() override
  {
    return optStrings;
  }

  virtual const wstring& Description() override
  {
    return description;
  }

  virtual bool HasParam()
  {
    return false;
  }

  virtual const wstring& Param()
  {
    // this function should never be called
    static const wstring empty(L"");
    return empty;
  }

  virtual void Call(const wstring& arg) override
  {
    UNREFERENCED_PARAMETER(arg);
  }

  virtual void Call() override
  {
    func();
  }
};

template <typename Function>
class ParamOption : public _Option
{
  vector<wstring> optStrings;
  const Function& func;
  wstring description;
  wstring paramName;

  ParamOption& operator=(const ParamOption&) = delete;

public:

  ParamOption(vector<wstring> s, const wstring& param, const Function& f, const wstring& desc) :optStrings(move(s)), func(f), description(desc), paramName(param)
  {
  }

  virtual const vector<wstring>& Strings() override
  {
    return optStrings;
  }

  virtual const wstring& Description() override
  {
    return description;
  }

  virtual bool HasParam()
  {
    return true;
  }

  virtual const wstring& Param()
  {
    return paramName;
  }

  virtual void Call(const wstring& arg) override
  {
    func(arg);
  }

  virtual void Call() override
  {
  }
};

template <typename Function>
_Option* make_option(vector<wstring> s, const Function& f, const wstring& desc)
{
  return (_Option*)new Option<Function>(s, f, desc);
}

template <typename Function>
_Option* make_option(vector<wstring> s, const wstring& param, const Function& f, const wstring& desc)
{
  return (_Option*)new ParamOption<Function>(s, param, f, desc);
}

class OptionList
{
  vector<unique_ptr<_Option>> options;
  wstring exeName;
  wstring positionalArgs;
  wstring positionalArgInfo;

public:

  OptionList(const wstring& exe, const wstring& positionalArgs, const wstring& positionalArgInfo) :exeName(exe), positionalArgs(positionalArgs), positionalArgInfo(positionalArgInfo)
  {
  }

  template <typename Function>
  void Add(vector<wstring> s, const Function& f, const wstring& desc)
  {
    Add(make_option(s, f, desc));
  }

  template <typename Function>
  void Add(vector<wstring> s, const wstring& param, const Function& f, const wstring& desc)
  {
    Add(make_option(s, param, f, desc));
  }

  void Add(_Option* option)
  {
    options.push_back(unique_ptr<_Option>(option));
  }

  wstring UsageShort()
  {
    wstringstream ret;
    for (const unique_ptr<_Option>& option : options)
    {
      ret << L" [";
      bool first = true;
      for (auto str : option->Strings())
      {
        if (first)
          first = false;
        else
          ret << L"|";
        ret << L"[-" << str << L"]";
      }
      if (option->HasParam())
        ret << L" <" << option->Param() << L">";
      ret << L"]";
    }
    return ret.str();
  }

  wstring UsageLong()
  {
    wstringstream ret;
    for (const unique_ptr<_Option>& option : options)
    {
      ret << L"\t";
      bool first = true;
      for (auto str : option->Strings())
      {
        if (first)
          first = false;
        else
          ret << L", ";
        ret << L"-" << str;
      }
      if (option->HasParam())
        ret << L" <" << option->Param() << L">";
      ret << L"\n\t\t" << option->Description() << L"\n";
    }
    return ret.str();
  }

  wstring Usage()
  {
    return exeName + L" " + UsageShort() + L" " + positionalArgs + L"\n\nOptions:\n" + UsageLong() + L"\nArguments:\n" + positionalArgInfo;
  }

  template <typename ErrorFunction>
  wchar_t** Parse(int argc, wchar_t** argv, ErrorFunction onError)
  {
    if (argc == 0)
      return argv;
    if ((*argv)[0] != '-' && (*argv)[0] != '/')
      return argv;
    if (_Parse(&argc, &argv, onError))
    {
      return Parse(argc, argv, onError);
    }
    else
    {
      return argv;
    }
  }

  void PrintUsage()
  {
    wprintf_s(L"%s", Usage().c_str());
  }
private:
  template <typename ErrorFunction>
  bool _Parse(int* argc, wchar_t*** argv, ErrorFunction onError)
  {
    for (const unique_ptr<_Option>& option : options)
    {
      if (argc == 0)
        return false;
      wchar_t* curParam = **argv;
      for (auto str : option->Strings())
      {
        if (str.compare(curParam + 1) == 0)
        {
          if (option->HasParam())
          {
            if (*argc < 2)
            {
              onError(str);
              return false;
            }
            option->Call(*(*argv + 1));
            *argv += 2;
            *argc -= 2;
            return true;
          }
          else
          {
            option->Call();
            *argv += 1;
            *argc -= 1;
            return true;
          }
        }
      }
    }
    return false;
  }
};

/*
template <typename T>
struct OptionWithParam
{
    OptionWithParam(vector<wstring> s, const T& f) :optStrings(move(s)), func(f)
    {
    }
    OptionWithParam<T>& withDescription(wstring desc, wstring paramName)
    {
        description = desc;
        this->paramName = paramName;
        return *this;
    }
    vector<wstring> optStrings;
    const T& func;
    wstring description;
    wstring paramName;
};*/

/*
template <typename... Options>
struct OptionList;

wstring _Usage(OptionList<>* list);

template <>
struct OptionList<>
{
    template <typename err>
    wchar_t** Parse(int argc, wchar_t** argv, err)
    {
        return argv;
    }
    wstring Usage()
    {
        return _Usage(this);
    }
    void PrintUsage()
    {
        wprintf_s(L"%s", Usage().c_str());
    }
    OptionList<>& withInfo(const wstring& exeName, const wstring& positionalArgs, const wstring& positionalArgInfo)
    {
        this->exeName = exeName;
        this->positionalArgs = positionalArgs;
        this->positionalArgInfo = positionalArgInfo;
        return *this;
    }
protected:
    template <typename err>
    bool _Parse(int& argc, wchar_t**& argv, err)
    {
        return false;
    }
    virtual wstring UsageLong()
    {
        return L"";
    }
    virtual wstring UsageShort()
    {
        return L"";
    }
    wstring exeName;
    wstring positionalArgs;
    wstring positionalArgInfo;
    friend wstring _Usage(OptionList<>* list);
};


template <typename T, typename... Tail>
struct OptionList<Option<T>, Tail...> : public OptionList<Tail...>
{
    OptionList(Option<T> c, Tail... t) : OptionList<Tail...>(t...), curOpt(c)
    {}
    template <typename err>
    wchar_t** Parse(int argc, wchar_t** argv, err onError)
    {
        if (argc == 0)
            return argv;
        if ((*argv)[0] != '-' && (*argv)[0] != '/')
            return argv;
        if (_Parse(argc, argv, onError))
        {
            return Parse(argc, argv, onError);
        }
        else
        {
            return argv;
        }
    }
    OptionList<Option<T>, Tail...>& withInfo(const wstring& exeName, const wstring& positionalArgs, const wstring& positionalArgInfo)
    {
        this->exeName = exeName;
        this->positionalArgs = positionalArgs;
        this->positionalArgInfo = positionalArgInfo;
        return *this;
    }
    wstring Usage()
    {
        return _Usage(this);
    }
    void PrintUsage()
    {
        wprintf_s(L"%s", Usage().c_str());
    }
protected:
    template <typename err>
    bool _Parse(int& argc, wchar_t**& argv, err onError)
    {
        if (argc == 0)
            return false;
        wchar_t* curParam = *argv;
        for (auto opt : curOpt.optStrings)
        {
            if (opt.compare(curParam + 1) == 0)
            {
                curOpt.func();
                argv += 1;
                argc -= 1;
                return true;
            }
        }
        return __super::_Parse(argc, argv, onError);
    }
    virtual wstring UsageLong() override
    {
        wstring ret = L"\t";
        bool first = true;
        for (auto opt : curOpt.optStrings)
        {
            if (first)
                first = false;
            else
                ret += L", ";
            ret += L"-" + opt;
        }
        ret += L"\n\t\t" + curOpt.description + L"\n";
        return ret + __super::UsageLong();
    }
    virtual wstring UsageShort() override
    {
        wstring ret = L" [";
        bool first = true;
        for (auto opt : curOpt.optStrings)
        {
            if (first)
                first = false;
            else
                ret += L"|";
            ret += L"[-" + opt + L"]";
        }
        return ret + L"]" + __super::UsageShort();
    }
private:
    Option<T> curOpt;
};

template <typename T, typename... Tail>
struct OptionList<OptionWithParam<T>, Tail...> : public OptionList<Tail...>
{
    OptionList(OptionWithParam<T> c, Tail... t) : OptionList<Tail...>(t...), curOpt(c)
    {}
    template <typename err>
    wchar_t** Parse(int argc, wchar_t** argv, err onError)
    {
        if (argc == 0)
            return argv;
        if ((*argv)[0] != '-' && (*argv)[0] != '/')
            return argv;
        if (_Parse(argc, argv, onError))
        {
            return Parse(argc, argv, onError);
        }
        else
        {
            return argv;
        }
    }
    OptionList<OptionWithParam<T>, Tail...>& withInfo(const wstring& exeName, const wstring& positionalArgs, const wstring& positionalArgInfo)
    {
        this->exeName = exeName;
        this->positionalArgs = positionalArgs;
        this->positionalArgInfo = positionalArgInfo;
        return *this;
    }
    wstring Usage()
    {
        return _Usage(this);
    }
    void PrintUsage()
    {
        wprintf_s(L"%s", Usage().c_str());
    }
protected:
    template <typename err>
    bool _Parse(int& argc, wchar_t**& argv, err onError)
    {
        if (argc == 0)
            return false;
        wchar_t* curParam = *argv;
        for (auto opt : curOpt.optStrings)
        {
            if (opt.compare(curParam + 1) == 0)
            {
                if (argc < 2)
                {
                    onError(opt);
                    return false;
                }
                curOpt.func(*(argv + 1));
                argv += 2;
                argc -= 2;
                return true;
            }
        }
        return __super::_Parse(argc, argv, onError);
    }
private:
    OptionWithParam<T> curOpt;
};

template <typename... List>
OptionList<List...> option_list(List... opts)
{
    return OptionList<List...>(opts...);
}

template <typename Func>
Option<Func> make_opt(vector<wstring> s, const Func& f)
{
    return Option<Func>(s, f);
}

template <typename Func>
OptionWithParam<Func> make_arg(vector<wstring> s, const Func& f)
{
    return OptionWithParam<Func>(s, f);
}

wstring _Usage(OptionList<>* list)
{
    return list->exeName + L" " + list->UsageShort() + L" " + list->positionalArgs + L"\n\nOptions:\n" + list->UsageLong() + L"\nArguments:\n" + list->positionalArgInfo;
}*/
