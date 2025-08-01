//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class CallableBond : Bond {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal CallableBond(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.CallableBond_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(CallableBond obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_CallableBond(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public CallabilitySchedule callability() {
    CallabilitySchedule ret = new CallabilitySchedule(NQuantLibcPINVOKE.CallableBond_callability(swigCPtr), false);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double impliedVolatility(BondPrice targetPrice, YieldTermStructureHandle discountCurve, double accuracy, uint maxEvaluations, double minVol, double maxVol) {
    double ret = NQuantLibcPINVOKE.CallableBond_impliedVolatility(swigCPtr, BondPrice.getCPtr(targetPrice), YieldTermStructureHandle.getCPtr(discountCurve), accuracy, maxEvaluations, minVol, maxVol);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double OAS(double cleanPrice, YieldTermStructureHandle engineTS, DayCounter dc, Compounding compounding, Frequency freq, Date settlementDate, double accuracy, uint maxIterations, double guess) {
    double ret = NQuantLibcPINVOKE.CallableBond_OAS__SWIG_0(swigCPtr, cleanPrice, YieldTermStructureHandle.getCPtr(engineTS), DayCounter.getCPtr(dc), (int)compounding, (int)freq, Date.getCPtr(settlementDate), accuracy, maxIterations, guess);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double OAS(double cleanPrice, YieldTermStructureHandle engineTS, DayCounter dc, Compounding compounding, Frequency freq, Date settlementDate, double accuracy, uint maxIterations) {
    double ret = NQuantLibcPINVOKE.CallableBond_OAS__SWIG_1(swigCPtr, cleanPrice, YieldTermStructureHandle.getCPtr(engineTS), DayCounter.getCPtr(dc), (int)compounding, (int)freq, Date.getCPtr(settlementDate), accuracy, maxIterations);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double OAS(double cleanPrice, YieldTermStructureHandle engineTS, DayCounter dc, Compounding compounding, Frequency freq, Date settlementDate, double accuracy) {
    double ret = NQuantLibcPINVOKE.CallableBond_OAS__SWIG_2(swigCPtr, cleanPrice, YieldTermStructureHandle.getCPtr(engineTS), DayCounter.getCPtr(dc), (int)compounding, (int)freq, Date.getCPtr(settlementDate), accuracy);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double OAS(double cleanPrice, YieldTermStructureHandle engineTS, DayCounter dc, Compounding compounding, Frequency freq, Date settlementDate) {
    double ret = NQuantLibcPINVOKE.CallableBond_OAS__SWIG_3(swigCPtr, cleanPrice, YieldTermStructureHandle.getCPtr(engineTS), DayCounter.getCPtr(dc), (int)compounding, (int)freq, Date.getCPtr(settlementDate));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double OAS(double cleanPrice, YieldTermStructureHandle engineTS, DayCounter dc, Compounding compounding, Frequency freq) {
    double ret = NQuantLibcPINVOKE.CallableBond_OAS__SWIG_4(swigCPtr, cleanPrice, YieldTermStructureHandle.getCPtr(engineTS), DayCounter.getCPtr(dc), (int)compounding, (int)freq);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double cleanPriceOAS(double oas, YieldTermStructureHandle engineTS, DayCounter dayCounter, Compounding compounding, Frequency frequency, Date settlementDate) {
    double ret = NQuantLibcPINVOKE.CallableBond_cleanPriceOAS__SWIG_0(swigCPtr, oas, YieldTermStructureHandle.getCPtr(engineTS), DayCounter.getCPtr(dayCounter), (int)compounding, (int)frequency, Date.getCPtr(settlementDate));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double cleanPriceOAS(double oas, YieldTermStructureHandle engineTS, DayCounter dayCounter, Compounding compounding, Frequency frequency) {
    double ret = NQuantLibcPINVOKE.CallableBond_cleanPriceOAS__SWIG_1(swigCPtr, oas, YieldTermStructureHandle.getCPtr(engineTS), DayCounter.getCPtr(dayCounter), (int)compounding, (int)frequency);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double effectiveDuration(double oas, YieldTermStructureHandle engineTS, DayCounter dayCounter, Compounding compounding, Frequency frequency, double bump) {
    double ret = NQuantLibcPINVOKE.CallableBond_effectiveDuration__SWIG_0(swigCPtr, oas, YieldTermStructureHandle.getCPtr(engineTS), DayCounter.getCPtr(dayCounter), (int)compounding, (int)frequency, bump);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double effectiveDuration(double oas, YieldTermStructureHandle engineTS, DayCounter dayCounter, Compounding compounding, Frequency frequency) {
    double ret = NQuantLibcPINVOKE.CallableBond_effectiveDuration__SWIG_1(swigCPtr, oas, YieldTermStructureHandle.getCPtr(engineTS), DayCounter.getCPtr(dayCounter), (int)compounding, (int)frequency);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double effectiveConvexity(double oas, YieldTermStructureHandle engineTS, DayCounter dayCounter, Compounding compounding, Frequency frequency, double bump) {
    double ret = NQuantLibcPINVOKE.CallableBond_effectiveConvexity__SWIG_0(swigCPtr, oas, YieldTermStructureHandle.getCPtr(engineTS), DayCounter.getCPtr(dayCounter), (int)compounding, (int)frequency, bump);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double effectiveConvexity(double oas, YieldTermStructureHandle engineTS, DayCounter dayCounter, Compounding compounding, Frequency frequency) {
    double ret = NQuantLibcPINVOKE.CallableBond_effectiveConvexity__SWIG_1(swigCPtr, oas, YieldTermStructureHandle.getCPtr(engineTS), DayCounter.getCPtr(dayCounter), (int)compounding, (int)frequency);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
