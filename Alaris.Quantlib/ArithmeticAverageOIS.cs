//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class ArithmeticAverageOIS : Swap {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal ArithmeticAverageOIS(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.ArithmeticAverageOIS_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(ArithmeticAverageOIS obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_ArithmeticAverageOIS(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public ArithmeticAverageOIS(Swap.Type type, double nominal, Schedule fixedLegSchedule, double fixedRate, DayCounter fixedDC, OvernightIndex overnightIndex, Schedule overnightLegSchedule, double spread, double meanReversionSpeed, double volatility, bool byApprox) : this(NQuantLibcPINVOKE.new_ArithmeticAverageOIS__SWIG_0((int)type, nominal, Schedule.getCPtr(fixedLegSchedule), fixedRate, DayCounter.getCPtr(fixedDC), OvernightIndex.getCPtr(overnightIndex), Schedule.getCPtr(overnightLegSchedule), spread, meanReversionSpeed, volatility, byApprox), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ArithmeticAverageOIS(Swap.Type type, double nominal, Schedule fixedLegSchedule, double fixedRate, DayCounter fixedDC, OvernightIndex overnightIndex, Schedule overnightLegSchedule, double spread, double meanReversionSpeed, double volatility) : this(NQuantLibcPINVOKE.new_ArithmeticAverageOIS__SWIG_1((int)type, nominal, Schedule.getCPtr(fixedLegSchedule), fixedRate, DayCounter.getCPtr(fixedDC), OvernightIndex.getCPtr(overnightIndex), Schedule.getCPtr(overnightLegSchedule), spread, meanReversionSpeed, volatility), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ArithmeticAverageOIS(Swap.Type type, double nominal, Schedule fixedLegSchedule, double fixedRate, DayCounter fixedDC, OvernightIndex overnightIndex, Schedule overnightLegSchedule, double spread, double meanReversionSpeed) : this(NQuantLibcPINVOKE.new_ArithmeticAverageOIS__SWIG_2((int)type, nominal, Schedule.getCPtr(fixedLegSchedule), fixedRate, DayCounter.getCPtr(fixedDC), OvernightIndex.getCPtr(overnightIndex), Schedule.getCPtr(overnightLegSchedule), spread, meanReversionSpeed), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ArithmeticAverageOIS(Swap.Type type, double nominal, Schedule fixedLegSchedule, double fixedRate, DayCounter fixedDC, OvernightIndex overnightIndex, Schedule overnightLegSchedule, double spread) : this(NQuantLibcPINVOKE.new_ArithmeticAverageOIS__SWIG_3((int)type, nominal, Schedule.getCPtr(fixedLegSchedule), fixedRate, DayCounter.getCPtr(fixedDC), OvernightIndex.getCPtr(overnightIndex), Schedule.getCPtr(overnightLegSchedule), spread), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ArithmeticAverageOIS(Swap.Type type, double nominal, Schedule fixedLegSchedule, double fixedRate, DayCounter fixedDC, OvernightIndex overnightIndex, Schedule overnightLegSchedule) : this(NQuantLibcPINVOKE.new_ArithmeticAverageOIS__SWIG_4((int)type, nominal, Schedule.getCPtr(fixedLegSchedule), fixedRate, DayCounter.getCPtr(fixedDC), OvernightIndex.getCPtr(overnightIndex), Schedule.getCPtr(overnightLegSchedule)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ArithmeticAverageOIS(Swap.Type type, DoubleVector nominals, Schedule fixedLegSchedule, double fixedRate, DayCounter fixedDC, OvernightIndex overnightIndex, Schedule overnightLegSchedule, double spread, double meanReversionSpeed, double volatility, bool byApprox) : this(NQuantLibcPINVOKE.new_ArithmeticAverageOIS__SWIG_5((int)type, DoubleVector.getCPtr(nominals), Schedule.getCPtr(fixedLegSchedule), fixedRate, DayCounter.getCPtr(fixedDC), OvernightIndex.getCPtr(overnightIndex), Schedule.getCPtr(overnightLegSchedule), spread, meanReversionSpeed, volatility, byApprox), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ArithmeticAverageOIS(Swap.Type type, DoubleVector nominals, Schedule fixedLegSchedule, double fixedRate, DayCounter fixedDC, OvernightIndex overnightIndex, Schedule overnightLegSchedule, double spread, double meanReversionSpeed, double volatility) : this(NQuantLibcPINVOKE.new_ArithmeticAverageOIS__SWIG_6((int)type, DoubleVector.getCPtr(nominals), Schedule.getCPtr(fixedLegSchedule), fixedRate, DayCounter.getCPtr(fixedDC), OvernightIndex.getCPtr(overnightIndex), Schedule.getCPtr(overnightLegSchedule), spread, meanReversionSpeed, volatility), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ArithmeticAverageOIS(Swap.Type type, DoubleVector nominals, Schedule fixedLegSchedule, double fixedRate, DayCounter fixedDC, OvernightIndex overnightIndex, Schedule overnightLegSchedule, double spread, double meanReversionSpeed) : this(NQuantLibcPINVOKE.new_ArithmeticAverageOIS__SWIG_7((int)type, DoubleVector.getCPtr(nominals), Schedule.getCPtr(fixedLegSchedule), fixedRate, DayCounter.getCPtr(fixedDC), OvernightIndex.getCPtr(overnightIndex), Schedule.getCPtr(overnightLegSchedule), spread, meanReversionSpeed), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ArithmeticAverageOIS(Swap.Type type, DoubleVector nominals, Schedule fixedLegSchedule, double fixedRate, DayCounter fixedDC, OvernightIndex overnightIndex, Schedule overnightLegSchedule, double spread) : this(NQuantLibcPINVOKE.new_ArithmeticAverageOIS__SWIG_8((int)type, DoubleVector.getCPtr(nominals), Schedule.getCPtr(fixedLegSchedule), fixedRate, DayCounter.getCPtr(fixedDC), OvernightIndex.getCPtr(overnightIndex), Schedule.getCPtr(overnightLegSchedule), spread), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ArithmeticAverageOIS(Swap.Type type, DoubleVector nominals, Schedule fixedLegSchedule, double fixedRate, DayCounter fixedDC, OvernightIndex overnightIndex, Schedule overnightLegSchedule) : this(NQuantLibcPINVOKE.new_ArithmeticAverageOIS__SWIG_9((int)type, DoubleVector.getCPtr(nominals), Schedule.getCPtr(fixedLegSchedule), fixedRate, DayCounter.getCPtr(fixedDC), OvernightIndex.getCPtr(overnightIndex), Schedule.getCPtr(overnightLegSchedule)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public Swap.Type type() {
    Swap.Type ret = (Swap.Type)NQuantLibcPINVOKE.ArithmeticAverageOIS_type(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double nominal() {
    double ret = NQuantLibcPINVOKE.ArithmeticAverageOIS_nominal(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public DoubleVector nominals() {
    DoubleVector ret = new DoubleVector(NQuantLibcPINVOKE.ArithmeticAverageOIS_nominals(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public Frequency fixedLegPaymentFrequency() {
    Frequency ret = (Frequency)NQuantLibcPINVOKE.ArithmeticAverageOIS_fixedLegPaymentFrequency(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public Frequency overnightLegPaymentFrequency() {
    Frequency ret = (Frequency)NQuantLibcPINVOKE.ArithmeticAverageOIS_overnightLegPaymentFrequency(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double fixedRate() {
    double ret = NQuantLibcPINVOKE.ArithmeticAverageOIS_fixedRate(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public DayCounter fixedDayCount() {
    DayCounter ret = new DayCounter(NQuantLibcPINVOKE.ArithmeticAverageOIS_fixedDayCount(swigCPtr), false);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public OvernightIndex overnightIndex() {
    global::System.IntPtr cPtr = NQuantLibcPINVOKE.ArithmeticAverageOIS_overnightIndex(swigCPtr);
    OvernightIndex ret = (cPtr == global::System.IntPtr.Zero) ? null : new OvernightIndex(cPtr, true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double spread() {
    double ret = NQuantLibcPINVOKE.ArithmeticAverageOIS_spread(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public Leg fixedLeg() {
    Leg ret = new Leg(NQuantLibcPINVOKE.ArithmeticAverageOIS_fixedLeg(swigCPtr), false);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public Leg overnightLeg() {
    Leg ret = new Leg(NQuantLibcPINVOKE.ArithmeticAverageOIS_overnightLeg(swigCPtr), false);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double fixedLegBPS() {
    double ret = NQuantLibcPINVOKE.ArithmeticAverageOIS_fixedLegBPS(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double fixedLegNPV() {
    double ret = NQuantLibcPINVOKE.ArithmeticAverageOIS_fixedLegNPV(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double fairRate() {
    double ret = NQuantLibcPINVOKE.ArithmeticAverageOIS_fairRate(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double overnightLegBPS() {
    double ret = NQuantLibcPINVOKE.ArithmeticAverageOIS_overnightLegBPS(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double overnightLegNPV() {
    double ret = NQuantLibcPINVOKE.ArithmeticAverageOIS_overnightLegNPV(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double fairSpread() {
    double ret = NQuantLibcPINVOKE.ArithmeticAverageOIS_fairSpread(swigCPtr);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
