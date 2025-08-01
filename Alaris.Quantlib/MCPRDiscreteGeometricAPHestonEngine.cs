//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class MCPRDiscreteGeometricAPHestonEngine : PricingEngine {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal MCPRDiscreteGeometricAPHestonEngine(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.MCPRDiscreteGeometricAPHestonEngine_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(MCPRDiscreteGeometricAPHestonEngine obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_MCPRDiscreteGeometricAPHestonEngine(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public MCPRDiscreteGeometricAPHestonEngine(HestonProcess process, bool antitheticVariate, int requiredSamples, double requiredTolerance, int maxSamples, int seed, int timeSteps, int timeStepsPerYear) : this(NQuantLibcPINVOKE.new_MCPRDiscreteGeometricAPHestonEngine__SWIG_0(HestonProcess.getCPtr(process), antitheticVariate, requiredSamples, requiredTolerance, maxSamples, seed, timeSteps, timeStepsPerYear), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MCPRDiscreteGeometricAPHestonEngine(HestonProcess process, bool antitheticVariate, int requiredSamples, double requiredTolerance, int maxSamples, int seed, int timeSteps) : this(NQuantLibcPINVOKE.new_MCPRDiscreteGeometricAPHestonEngine__SWIG_1(HestonProcess.getCPtr(process), antitheticVariate, requiredSamples, requiredTolerance, maxSamples, seed, timeSteps), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MCPRDiscreteGeometricAPHestonEngine(HestonProcess process, bool antitheticVariate, int requiredSamples, double requiredTolerance, int maxSamples, int seed) : this(NQuantLibcPINVOKE.new_MCPRDiscreteGeometricAPHestonEngine__SWIG_2(HestonProcess.getCPtr(process), antitheticVariate, requiredSamples, requiredTolerance, maxSamples, seed), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MCPRDiscreteGeometricAPHestonEngine(HestonProcess process, bool antitheticVariate, int requiredSamples, double requiredTolerance, int maxSamples) : this(NQuantLibcPINVOKE.new_MCPRDiscreteGeometricAPHestonEngine__SWIG_3(HestonProcess.getCPtr(process), antitheticVariate, requiredSamples, requiredTolerance, maxSamples), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MCPRDiscreteGeometricAPHestonEngine(HestonProcess process, bool antitheticVariate, int requiredSamples, double requiredTolerance) : this(NQuantLibcPINVOKE.new_MCPRDiscreteGeometricAPHestonEngine__SWIG_4(HestonProcess.getCPtr(process), antitheticVariate, requiredSamples, requiredTolerance), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MCPRDiscreteGeometricAPHestonEngine(HestonProcess process, bool antitheticVariate, int requiredSamples) : this(NQuantLibcPINVOKE.new_MCPRDiscreteGeometricAPHestonEngine__SWIG_5(HestonProcess.getCPtr(process), antitheticVariate, requiredSamples), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MCPRDiscreteGeometricAPHestonEngine(HestonProcess process, bool antitheticVariate) : this(NQuantLibcPINVOKE.new_MCPRDiscreteGeometricAPHestonEngine__SWIG_6(HestonProcess.getCPtr(process), antitheticVariate), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public MCPRDiscreteGeometricAPHestonEngine(HestonProcess process) : this(NQuantLibcPINVOKE.new_MCPRDiscreteGeometricAPHestonEngine__SWIG_7(HestonProcess.getCPtr(process)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

}
