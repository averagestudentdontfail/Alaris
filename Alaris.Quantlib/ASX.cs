//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class ASX : global::System.IDisposable {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  protected bool swigCMemOwn;

  internal ASX(global::System.IntPtr cPtr, bool cMemoryOwn) {
    swigCMemOwn = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(ASX obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  internal static global::System.Runtime.InteropServices.HandleRef swigRelease(ASX obj) {
    if (obj != null) {
      if (!obj.swigCMemOwn)
        throw new global::System.ApplicationException("Cannot release ownership as memory is not owned");
      global::System.Runtime.InteropServices.HandleRef ptr = obj.swigCPtr;
      obj.swigCMemOwn = false;
      obj.Dispose();
      return ptr;
    } else {
      return new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
    }
  }

  ~ASX() {
    Dispose(false);
  }

  public void Dispose() {
    Dispose(true);
    global::System.GC.SuppressFinalize(this);
  }

  protected virtual void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwn) {
          swigCMemOwn = false;
          NQuantLibcPINVOKE.delete_ASX(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
    }
  }

  public static bool isASXdate(Date d, bool mainCycle) {
    bool ret = NQuantLibcPINVOKE.ASX_isASXdate__SWIG_0(Date.getCPtr(d), mainCycle);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static bool isASXdate(Date d) {
    bool ret = NQuantLibcPINVOKE.ASX_isASXdate__SWIG_1(Date.getCPtr(d));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static bool isASXcode(string code, bool mainCycle) {
    bool ret = NQuantLibcPINVOKE.ASX_isASXcode__SWIG_0(code, mainCycle);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static bool isASXcode(string code) {
    bool ret = NQuantLibcPINVOKE.ASX_isASXcode__SWIG_1(code);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static string code(Date asxDate) {
    string ret = NQuantLibcPINVOKE.ASX_code(Date.getCPtr(asxDate));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static Date date(string asxCode, Date referenceDate) {
    Date ret = new Date(NQuantLibcPINVOKE.ASX_date__SWIG_0(asxCode, Date.getCPtr(referenceDate)), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static Date date(string asxCode) {
    Date ret = new Date(NQuantLibcPINVOKE.ASX_date__SWIG_1(asxCode), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static Date nextDate(Date d, bool mainCycle) {
    Date ret = new Date(NQuantLibcPINVOKE.ASX_nextDate__SWIG_0(Date.getCPtr(d), mainCycle), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static Date nextDate(Date d) {
    Date ret = new Date(NQuantLibcPINVOKE.ASX_nextDate__SWIG_1(Date.getCPtr(d)), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static Date nextDate() {
    Date ret = new Date(NQuantLibcPINVOKE.ASX_nextDate__SWIG_2(), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static Date nextDate(string asxCode, bool mainCycle, Date referenceDate) {
    Date ret = new Date(NQuantLibcPINVOKE.ASX_nextDate__SWIG_3(asxCode, mainCycle, Date.getCPtr(referenceDate)), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static Date nextDate(string asxCode, bool mainCycle) {
    Date ret = new Date(NQuantLibcPINVOKE.ASX_nextDate__SWIG_4(asxCode, mainCycle), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static Date nextDate(string asxCode) {
    Date ret = new Date(NQuantLibcPINVOKE.ASX_nextDate__SWIG_5(asxCode), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static string nextCode(Date d, bool mainCycle) {
    string ret = NQuantLibcPINVOKE.ASX_nextCode__SWIG_0(Date.getCPtr(d), mainCycle);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static string nextCode(Date d) {
    string ret = NQuantLibcPINVOKE.ASX_nextCode__SWIG_1(Date.getCPtr(d));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static string nextCode() {
    string ret = NQuantLibcPINVOKE.ASX_nextCode__SWIG_2();
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static string nextCode(string asxCode, bool mainCycle, Date referenceDate) {
    string ret = NQuantLibcPINVOKE.ASX_nextCode__SWIG_3(asxCode, mainCycle, Date.getCPtr(referenceDate));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static string nextCode(string asxCode, bool mainCycle) {
    string ret = NQuantLibcPINVOKE.ASX_nextCode__SWIG_4(asxCode, mainCycle);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static string nextCode(string asxCode) {
    string ret = NQuantLibcPINVOKE.ASX_nextCode__SWIG_5(asxCode);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public ASX() : this(NQuantLibcPINVOKE.new_ASX(), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public enum Month {
    F = 1,
    G = 2,
    H = 3,
    J = 4,
    K = 5,
    M = 6,
    N = 7,
    Q = 8,
    U = 9,
    V = 10,
    X = 11,
    Z = 12
  }

}
