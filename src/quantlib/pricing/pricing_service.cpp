#include "alo_engine.h"
#include <iostream>

namespace Alaris::Pricing {

class PricingService {
private:
    QuantLibALOEngine& engine_;
    
public:
    explicit PricingService(QuantLibALOEngine& engine) : engine_(engine) {}
    
    double price_option(const OptionData& option) {
        return engine_.calculate_option_price(option);
    }
    
    void batch_price_options(const std::vector<OptionData>& options, 
                            std::vector<double>& results) {
        engine_.batch_calculate_prices(options, results);
    }
};

// Global pricing service functions
void initialize_pricing_service() {
    std::cout << "Pricing service initialized" << std::endl;
}

void shutdown_pricing_service() {
    std::cout << "Pricing service shutdown" << std::endl;
}

} // namespace Alaris::Pricing
