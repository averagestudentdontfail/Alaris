//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class SurvivalProbabilityCurve : DefaultProbabilityTermStructure {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal SurvivalProbabilityCurve(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.SurvivalProbabilityCurve_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(SurvivalProbabilityCurve obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_SurvivalProbabilityCurve(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public SurvivalProbabilityCurve(DateVector dates, DoubleVector probabilities, DayCounter dayCounter, Calendar calendar, Linear i) : this(NQuantLibcPINVOKE.new_SurvivalProbabilityCurve__SWIG_0(DateVector.getCPtr(dates), DoubleVector.getCPtr(probabilities), DayCounter.getCPtr(dayCounter), Calendar.getCPtr(calendar), Linear.getCPtr(i)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public SurvivalProbabilityCurve(DateVector dates, DoubleVector probabilities, DayCounter dayCounter, Calendar calendar) : this(NQuantLibcPINVOKE.new_SurvivalProbabilityCurve__SWIG_1(DateVector.getCPtr(dates), DoubleVector.getCPtr(probabilities), DayCounter.getCPtr(dayCounter), Calendar.getCPtr(calendar)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public SurvivalProbabilityCurve(DateVector dates, DoubleVector probabilities, DayCounter dayCounter) : this(NQuantLibcPINVOKE.new_SurvivalProbabilityCurve__SWIG_2(DateVector.getCPtr(dates), DoubleVector.getCPtr(probabilities), DayCounter.getCPtr(dayCounter)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public DateVector dates() {
    DateVector ret = new DateVector(NQuantLibcPINVOKE.SurvivalProbabilityCurve_dates(swigCPtr), false);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public DoubleVector survivalProbabilities() {
    DoubleVector ret = new DoubleVector(NQuantLibcPINVOKE.SurvivalProbabilityCurve_survivalProbabilities(swigCPtr), false);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public NodeVector nodes() {
    NodeVector ret = new NodeVector(NQuantLibcPINVOKE.SurvivalProbabilityCurve_nodes(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
