//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class ZabrShortMaturityLognormalSmileSection : SmileSection {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal ZabrShortMaturityLognormalSmileSection(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.ZabrShortMaturityLognormalSmileSection_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(ZabrShortMaturityLognormalSmileSection obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_ZabrShortMaturityLognormalSmileSection(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public ZabrShortMaturityLognormalSmileSection(double timeToExpiry, double forward, DoubleVector zabrParameters, DoubleVector moneyness, uint fdRefinement) : this(NQuantLibcPINVOKE.new_ZabrShortMaturityLognormalSmileSection__SWIG_0(timeToExpiry, forward, DoubleVector.getCPtr(zabrParameters), DoubleVector.getCPtr(moneyness), fdRefinement), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ZabrShortMaturityLognormalSmileSection(double timeToExpiry, double forward, DoubleVector zabrParameters, DoubleVector moneyness) : this(NQuantLibcPINVOKE.new_ZabrShortMaturityLognormalSmileSection__SWIG_1(timeToExpiry, forward, DoubleVector.getCPtr(zabrParameters), DoubleVector.getCPtr(moneyness)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ZabrShortMaturityLognormalSmileSection(double timeToExpiry, double forward, DoubleVector zabrParameters) : this(NQuantLibcPINVOKE.new_ZabrShortMaturityLognormalSmileSection__SWIG_2(timeToExpiry, forward, DoubleVector.getCPtr(zabrParameters)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ZabrShortMaturityLognormalSmileSection(Date d, double forward, DoubleVector zabrParameters, DayCounter dc, DoubleVector moneyness, uint fdRefinement) : this(NQuantLibcPINVOKE.new_ZabrShortMaturityLognormalSmileSection__SWIG_3(Date.getCPtr(d), forward, DoubleVector.getCPtr(zabrParameters), DayCounter.getCPtr(dc), DoubleVector.getCPtr(moneyness), fdRefinement), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ZabrShortMaturityLognormalSmileSection(Date d, double forward, DoubleVector zabrParameters, DayCounter dc, DoubleVector moneyness) : this(NQuantLibcPINVOKE.new_ZabrShortMaturityLognormalSmileSection__SWIG_4(Date.getCPtr(d), forward, DoubleVector.getCPtr(zabrParameters), DayCounter.getCPtr(dc), DoubleVector.getCPtr(moneyness)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ZabrShortMaturityLognormalSmileSection(Date d, double forward, DoubleVector zabrParameters, DayCounter dc) : this(NQuantLibcPINVOKE.new_ZabrShortMaturityLognormalSmileSection__SWIG_5(Date.getCPtr(d), forward, DoubleVector.getCPtr(zabrParameters), DayCounter.getCPtr(dc)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ZabrShortMaturityLognormalSmileSection(Date d, double forward, DoubleVector zabrParameters) : this(NQuantLibcPINVOKE.new_ZabrShortMaturityLognormalSmileSection__SWIG_6(Date.getCPtr(d), forward, DoubleVector.getCPtr(zabrParameters)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

}
