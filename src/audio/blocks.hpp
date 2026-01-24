#pragma once

#include <vector>
#include <string>
#include <sstream>
#include <deque>

#pragma region Helpers

class Block {
public:
    virtual ~Block() = default;

    virtual float Process(float buffer) {return buffer;}
    virtual void Start() {}
};

class DelayLine {
public:
    std::vector<float> buffer;
    int size = 1;
    int index = 0;

    void Init(int samples) {
        size = std::max(1, samples);
        buffer.assign(size, 0.0f);
        index = 0;
    }

    float Read() const {
        return buffer[index];
    }

    void Write(float x) {
        buffer[index] = x;
        index = (index + 1) % size;
    }
};

class CombFilter {
public:
    DelayLine delay;
    float feedback = 0.7f;
    float damp = 0.5f;

    float filter_store = 0.0f;

    void Init(int delay_samples, float fb, float d) {
        delay.Init(delay_samples);
        feedback = fb;
        damp = d;
        filter_store = 0.0f;
    }

    float Process(float input) {
        float output = delay.Read();

        filter_store = output * (1.0f - damp) + filter_store * damp;
        delay.Write(input + filter_store * feedback);

        return output;
    }
};

class AllPassFilter {
public:
    DelayLine delay;
    float feedback = 0.5f;

    void Init(int delay_samples, float fb) {
        delay.Init(delay_samples);
        feedback = fb;
    }

    float Process(float input) {
        float buf = delay.Read();
        float output = -input + buf;
        delay.Write(input + buf * feedback);
        return output;
    }
};

#pragma endregion
#pragma region Classes

class DelayBlock : public Block {
public:
    float time_ms = 0.0f;
    float mix = 1.0f;
    int sample_rate = 44100;
    int delay_samples = 1;
    std::vector<float> buffer;
    int write_idx = 0;

    DelayBlock(float t_ms, int sr) : time_ms(t_ms), sample_rate(sr) {}

    void Start() override {
        delay_samples = std::max(1, int(time_ms * sample_rate / 1000.0f));
        buffer.assign(delay_samples, 0.0f);
        write_idx = 0;
    }

    float Process(float input) override {
        float delayed = buffer[write_idx];
        buffer[write_idx] = input;
        write_idx = (write_idx + 1) % delay_samples;

        return input * (1.0f - mix) + delayed * mix;
    }
};

class DistortionBlock : public Block {
public:
    float drive = 1.0f;

    DistortionBlock(float amount) {
        drive = 1.0f + amount * 20.0f;
    }

    float Process(float x) override {
        return std::tanh(x * drive);
    }
};

class CompressionBlock : public Block {
public:
    float threshold = 0.2f;
    float ratio = 4.0f;
    float env = 0.0f;

    float attack = 0.01f;
    float release = 0.1f;

    CompressionBlock(float a) : threshold(a) {}

    float Process(float x) override {
        float level = std::fabs(x);
        float coeff = level > env ? attack : release;
        env += coeff * (level - env);

        if (env <= threshold)
            return x;

        float gain = threshold + (env - threshold) / ratio;
        gain /= env;

        return x * gain;
    }
};

class GainBlock : public Block {
public:
    float amount;

    GainBlock(float t) : amount(t) {}

    float Process(float buffer) override {
        return buffer * amount;
    }
};

class GatingBlock : public Block {
public:
    float threshold = 0.05f;
    float env = 0.0f;
    float attack = 0.01f;
    float release = 0.05f;

    GatingBlock(float t) : threshold(t) {}

    float Process(float x) override {
        float level = std::fabs(x);
        float coeff = level > env ? attack : release;
        env += coeff * (level - env);

        return (env < threshold) ? 0.0f : x;
    }
};

class ReverbBlock : public Block {
public:
    int sample_rate = 44100;
    float room_size = 0.5f;
    float damp = 0.5f;
    float wet = 0.3f;
    float dry = 0.7f;

    static constexpr int NUM_COMBS = 4;
    static constexpr int NUM_ALLPASS = 2;

    CombFilter combs[NUM_COMBS];
    AllPassFilter allpass[NUM_ALLPASS];

    ReverbBlock(float intensity, int sr) : sample_rate(sr) {
        room_size = std::max(-1.0f, std::min(1.0f, intensity));
    }

    void Start() override {
        int comb_delays[NUM_COMBS] = { 1557, 1617, 1491, 1422 };
        int allpass_delays[NUM_ALLPASS] = { 225, 556 };

        for (int i = 0; i < NUM_COMBS; ++i) {
            int d = int(comb_delays[i] * room_size);
            combs[i].Init(d, 0.7f + room_size * 0.2f, damp);
        }

        for (int i = 0; i < NUM_ALLPASS; ++i) {
            allpass[i].Init(allpass_delays[i], 0.5f);
        }
    }

    float Process(float input) override {
        float sum = 0.0f;

        for (int i = 0; i < NUM_COMBS; ++i)
            sum += combs[i].Process(input);

        sum *= (1.0f / NUM_COMBS);

        for (int i = 0; i < NUM_ALLPASS; ++i)
            sum = allpass[i].Process(sum);

        return input * dry + sum * wet;
    }
};

#pragma endregion
#pragma region Manager

class BlocksManager {
public:
    void Initialize(const std::string& text, int sample_rate) {
        this->sample_rate = sample_rate;
        blocks.clear();

        std::istringstream input(text);
        std::string line;

        while (std::getline(input, line)) {
            if (!line.empty()) {
                blocks.push_back(CreateBlockFromLine(line));
            }
        }

        for (size_t i = 0; i < blocks.size(); ++i) {
            blocks.at(i).get()->Start();
        }
    }

    float Process(float* buffer) {
        float current_buffer = *buffer;

        for (size_t i = 0; i < blocks.size(); ++i) {
            current_buffer = blocks.at(i).get()->Process(current_buffer);
        }

        return current_buffer;
    }

private:
    int sample_rate;
    std::vector<std::unique_ptr<Block>> blocks;

    std::unique_ptr<Block> CreateBlockFromLine(const std::string& line) {
        std::istringstream iss(line);
        std::string type;
        iss >> type;

        std::unordered_map<std::string, float> params;
        std::string token;

        while (iss >> token) {
            auto pos = token.find("=");
            if (pos != std::string::npos) {
                params[token.substr(0,pos)] = std::stof(token.substr(pos+1));
            }
        }

        if (type == "delay")
            return std::make_unique<DelayBlock>(static_cast<int>(params.at("time")), sample_rate);
        if (type == "distortion")
            return std::make_unique<DistortionBlock>(params.at("intensity"));
        if (type == "compression")
            return std::make_unique<CompressionBlock>(params.at("amount"));
        if (type == "gating")
            return std::make_unique<GatingBlock>(params.at("threshold"));
        if (type == "reverb")
            return std::make_unique<ReverbBlock>(params.at("intensity"), sample_rate);
        if (type == "gain")
            return std::make_unique<GainBlock>(params.at("amount"));

        return std::make_unique<DelayBlock>(0, sample_rate);
    }
};