#pragma once

#include <vector>
#include <string>
#include <unordered_map>
#include <sstream>
#include <memory>
#include <stdexcept>
#include <list>
#include <deque>

class Block {
public:
    virtual ~Block() = default;

    virtual float Render(float buffer) {return buffer;}
};

class DelayBlock : public Block {
public:
    int time_ms;
    int sample_rate;
    int delay_samples;
    std::deque<float> buffers;

    DelayBlock(int t, int sr) : time_ms(t), sample_rate(sr), delay_samples(t * sr / 1000) {}

    float Render(float buffer) override {
        if (time_ms == 0)
            return buffer;

        this->buffers.push_back(buffer);

        if (this->buffers.size() <= delay_samples)
            return 0.0f;

        float out = this->buffers.front();
        this->buffers.pop_front();

        return out;
    }
};

class DistortionBlock : public Block {
public:
    float intensity;

    DistortionBlock(float i) : intensity(i) {}

    float Render(float buffer) override {
        if (intensity <= 0.0f)
            return buffer;

        float drive = 1.0f + intensity * 99.0f;
        float x = buffer * drive;
        return std::tanh(x) / std::tanh(drive);
    }
};

class CompressionBlock : public Block {
public:
    float amount;

    CompressionBlock(float a) : amount(a) {}

    float Render(float buffer) override {
        float absInput = std::fabs(buffer);

        if (amount <= 0.0f || absInput < 1e-6f)
            return buffer;

        float threshold = amount * 0.1f;

        if (absInput <= threshold)
            return buffer;

        float ratio = 1.0f + amount * 99.0f;

        float excess = absInput - threshold;
        float compressed = threshold + excess / ratio;

        return buffer * (compressed / absInput);
    }
};

class GainBlock : public Block {
public:
    float amount;

    GainBlock(float t) : amount(t) {}

    float Render(float buffer) override {
        float sample = buffer * amount;

        sample = std::max(-1.0f, std::min(1.0f, sample));
        return sample;
    }
};

class GatingBlock : public Block {
public:
    float threshold;

    GatingBlock(float t) : threshold(t) {}

    float Render(float buffer) override {
        if (std::fabs(buffer) < threshold)
            return 0.0f;

        return buffer;
    }
};

class ReverbBlock : public Block {
public:
    float intensity;
    int sample_rate;
    int delay_samples;
    float feedback;
    std::deque<float> buffer;

    ReverbBlock(float i, int sr) : intensity(i), sample_rate(sr), delay_samples((int)(i * 2000.0f * sr / 1000.0f)), feedback(i * 0.8f) {
        delay_samples = std::max(1, delay_samples);
    }

    float Render(float buffer) override {
        float delayed = 0.0f;

        if (this->buffer.size() >= delay_samples) {
            delayed = this->buffer.front();
            this->buffer.pop_front();
        }

        float feedback_sample = buffer + delayed * feedback;
        this->buffer.push_back(feedback_sample);

        float output = buffer + delayed * 0.5f;
        return output;
    }
};

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
    }

    float Render(float* buffer) {
        float current_buffer = *buffer;

        for (size_t i = 0; i < blocks.size(); ++i) {
            current_buffer = blocks.at(i).get()->Render(current_buffer);
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