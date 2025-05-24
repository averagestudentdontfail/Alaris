#pragma once

#include "alo_engine.h"

namespace Alaris::Pricing {

class PricingService {
public:
    explicit PricingService(QuantLibALOEngine& engine);
    
    double price_option(const OptionData& option);
    void batch_price_options(const std::vector<OptionData>& options, 
                            std::vector<double>& results);
                            
private:
    QuantLibALOEngine& engine_;
};

// Service management functions
void initialize_pricing_service();
void shutdown_pricing_service();

} // namespace Alaris::Pricing
