//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class InterestRateIndex : Index {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal InterestRateIndex(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.InterestRateIndex_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(InterestRateIndex obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_InterestRateIndex(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public string familyName() {
    string ret = NQuantLibcPINVOKE.InterestRateIndex_familyName(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public Period tenor() {
    Period ret = new Period(NQuantLibcPINVOKE.InterestRateIndex_tenor(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public uint fixingDays() {
    uint ret = NQuantLibcPINVOKE.InterestRateIndex_fixingDays(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public Date fixingDate(Date valueDate) {
    Date ret = new Date(NQuantLibcPINVOKE.InterestRateIndex_fixingDate(swigCPtr, Date.getCPtr(valueDate)), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public Currency currency() {
    Currency ret = new Currency(NQuantLibcPINVOKE.InterestRateIndex_currency(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public DayCounter dayCounter() {
    DayCounter ret = new DayCounter(NQuantLibcPINVOKE.InterestRateIndex_dayCounter(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public Date maturityDate(Date valueDate) {
    Date ret = new Date(NQuantLibcPINVOKE.InterestRateIndex_maturityDate(swigCPtr, Date.getCPtr(valueDate)), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public Date valueDate(Date fixingDate) {
    Date ret = new Date(NQuantLibcPINVOKE.InterestRateIndex_valueDate(swigCPtr, Date.getCPtr(fixingDate)), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
