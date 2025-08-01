//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class NoArbSabrSmileSection : SmileSection {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal NoArbSabrSmileSection(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.NoArbSabrSmileSection_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(NoArbSabrSmileSection obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_NoArbSabrSmileSection(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public NoArbSabrSmileSection(double timeToExpiry, double forward, DoubleVector sabrParameters, double shift, VolatilityType volatilityType) : this(NQuantLibcPINVOKE.new_NoArbSabrSmileSection__SWIG_0(timeToExpiry, forward, DoubleVector.getCPtr(sabrParameters), shift, (int)volatilityType), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public NoArbSabrSmileSection(double timeToExpiry, double forward, DoubleVector sabrParameters, double shift) : this(NQuantLibcPINVOKE.new_NoArbSabrSmileSection__SWIG_1(timeToExpiry, forward, DoubleVector.getCPtr(sabrParameters), shift), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public NoArbSabrSmileSection(double timeToExpiry, double forward, DoubleVector sabrParameters) : this(NQuantLibcPINVOKE.new_NoArbSabrSmileSection__SWIG_2(timeToExpiry, forward, DoubleVector.getCPtr(sabrParameters)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public NoArbSabrSmileSection(Date d, double forward, DoubleVector sabrParameters, DayCounter dc, double shift, VolatilityType volatilityType) : this(NQuantLibcPINVOKE.new_NoArbSabrSmileSection__SWIG_3(Date.getCPtr(d), forward, DoubleVector.getCPtr(sabrParameters), DayCounter.getCPtr(dc), shift, (int)volatilityType), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public NoArbSabrSmileSection(Date d, double forward, DoubleVector sabrParameters, DayCounter dc, double shift) : this(NQuantLibcPINVOKE.new_NoArbSabrSmileSection__SWIG_4(Date.getCPtr(d), forward, DoubleVector.getCPtr(sabrParameters), DayCounter.getCPtr(dc), shift), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public NoArbSabrSmileSection(Date d, double forward, DoubleVector sabrParameters, DayCounter dc) : this(NQuantLibcPINVOKE.new_NoArbSabrSmileSection__SWIG_5(Date.getCPtr(d), forward, DoubleVector.getCPtr(sabrParameters), DayCounter.getCPtr(dc)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public NoArbSabrSmileSection(Date d, double forward, DoubleVector sabrParameters) : this(NQuantLibcPINVOKE.new_NoArbSabrSmileSection__SWIG_6(Date.getCPtr(d), forward, DoubleVector.getCPtr(sabrParameters)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

}
