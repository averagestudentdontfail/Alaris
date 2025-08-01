//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class SabrSmileSection : SmileSection {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal SabrSmileSection(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.SabrSmileSection_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(SabrSmileSection obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_SabrSmileSection(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public SabrSmileSection(Date d, double forward, DoubleVector sabrParameters, Date referenceDate, DayCounter dc, double shift, VolatilityType volatilityType) : this(NQuantLibcPINVOKE.new_SabrSmileSection__SWIG_0(Date.getCPtr(d), forward, DoubleVector.getCPtr(sabrParameters), Date.getCPtr(referenceDate), DayCounter.getCPtr(dc), shift, (int)volatilityType), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public SabrSmileSection(Date d, double forward, DoubleVector sabrParameters, Date referenceDate, DayCounter dc, double shift) : this(NQuantLibcPINVOKE.new_SabrSmileSection__SWIG_1(Date.getCPtr(d), forward, DoubleVector.getCPtr(sabrParameters), Date.getCPtr(referenceDate), DayCounter.getCPtr(dc), shift), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public SabrSmileSection(Date d, double forward, DoubleVector sabrParameters, Date referenceDate, DayCounter dc) : this(NQuantLibcPINVOKE.new_SabrSmileSection__SWIG_2(Date.getCPtr(d), forward, DoubleVector.getCPtr(sabrParameters), Date.getCPtr(referenceDate), DayCounter.getCPtr(dc)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public SabrSmileSection(Date d, double forward, DoubleVector sabrParameters, Date referenceDate) : this(NQuantLibcPINVOKE.new_SabrSmileSection__SWIG_3(Date.getCPtr(d), forward, DoubleVector.getCPtr(sabrParameters), Date.getCPtr(referenceDate)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public SabrSmileSection(Date d, double forward, DoubleVector sabrParameters) : this(NQuantLibcPINVOKE.new_SabrSmileSection__SWIG_4(Date.getCPtr(d), forward, DoubleVector.getCPtr(sabrParameters)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public SabrSmileSection(double timeToExpiry, double forward, DoubleVector sabrParameters, double shift, VolatilityType volatilityType) : this(NQuantLibcPINVOKE.new_SabrSmileSection__SWIG_5(timeToExpiry, forward, DoubleVector.getCPtr(sabrParameters), shift, (int)volatilityType), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public SabrSmileSection(double timeToExpiry, double forward, DoubleVector sabrParameters, double shift) : this(NQuantLibcPINVOKE.new_SabrSmileSection__SWIG_6(timeToExpiry, forward, DoubleVector.getCPtr(sabrParameters), shift), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public SabrSmileSection(double timeToExpiry, double forward, DoubleVector sabrParameters) : this(NQuantLibcPINVOKE.new_SabrSmileSection__SWIG_7(timeToExpiry, forward, DoubleVector.getCPtr(sabrParameters)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public double alpha() {
    double ret = NQuantLibcPINVOKE.SabrSmileSection_alpha(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double beta() {
    double ret = NQuantLibcPINVOKE.SabrSmileSection_beta(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double nu() {
    double ret = NQuantLibcPINVOKE.SabrSmileSection_nu(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double rho() {
    double ret = NQuantLibcPINVOKE.SabrSmileSection_rho(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
